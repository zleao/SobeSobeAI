// Main Bicep template for SobeSobe infrastructure
targetScope = 'subscription'

@description('The environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string

@description('The Azure region for all resources')
param location string = 'westeurope'

@description('The unique suffix for resource names')
param resourceSuffix string = uniqueString(subscription().id, environmentName)

// Resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-sobesobe-${environmentName}'
  location: location
  tags: {
    Environment: environmentName
    Application: 'SobeSobe'
    ManagedBy: 'Bicep'
  }
}

// Key Vault for secrets
module keyVault 'modules/key-vault.bicep' = {
  scope: rg
  name: 'keyVaultDeployment'
  params: {
    environmentName: environmentName
    location: location
    resourceSuffix: resourceSuffix
  }
}

// App Service Plan and App Service for backend API
module appService 'modules/app-service.bicep' = {
  scope: rg
  name: 'appServiceDeployment'
  params: {
    environmentName: environmentName
    location: location
    resourceSuffix: resourceSuffix
    keyVaultName: keyVault.outputs.keyVaultName
  }
}

// Static Web App for frontend
module staticWebApp 'modules/static-web-app.bicep' = {
  scope: rg
  name: 'staticWebAppDeployment'
  params: {
    environmentName: environmentName
    location: location
    resourceSuffix: resourceSuffix
  }
}

// Application Insights for monitoring
module monitoring 'modules/monitoring.bicep' = {
  scope: rg
  name: 'monitoringDeployment'
  params: {
    environmentName: environmentName
    location: location
    resourceSuffix: resourceSuffix
  }
}

// SQLite used for dev/staging, Azure SQL for production (optional)
module database 'modules/database.bicep' = if (environmentName == 'prod') {
  scope: rg
  name: 'databaseDeployment'
  params: {
    environmentName: environmentName
    location: location
    resourceSuffix: resourceSuffix
    keyVaultName: keyVault.outputs.keyVaultName
  }
}

// Outputs
output resourceGroupName string = rg.name
output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
output staticWebAppUrl string = staticWebApp.outputs.staticWebAppUrl
output keyVaultName string = keyVault.outputs.keyVaultName
output applicationInsightsName string = monitoring.outputs.applicationInsightsName
output applicationInsightsInstrumentationKey string = monitoring.outputs.instrumentationKey
output databaseServerName string = environmentName == 'prod' ? database.outputs.serverName : 'SQLite'
output databaseName string = environmentName == 'prod' ? database.outputs.databaseName : 'Local SQLite file'
