// Static Web App module for frontend
param environmentName string
param location string
param resourceSuffix string

// SKU configuration per environment
var skuMap = {
  dev: 'Free'
  staging: 'Standard'
  prod: 'Standard'
}

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: 'swa-sobesobe-${resourceSuffix}'
  location: location
  tags: {
    Environment: environmentName
  }
  sku: {
    name: skuMap[environmentName]
    tier: skuMap[environmentName]
  }
  properties: {
    repositoryUrl: 'https://github.com/zleao/SobeSobeAI'
    branch: environmentName == 'prod' ? 'main' : environmentName
    buildProperties: {
      appLocation: '/frontend'
      apiLocation: ''
      outputLocation: 'dist/sobesobe/browser'
    }
  }
}

output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output staticWebAppId string = staticWebApp.id
