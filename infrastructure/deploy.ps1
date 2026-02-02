#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy SobeSobe infrastructure to Azure using Bicep.

.DESCRIPTION
    This script deploys the SobeSobe application infrastructure to Azure.
    It validates the Bicep template, creates the deployment, and outputs key resource information.

.PARAMETER Environment
    The environment to deploy (dev, staging, prod). Default: dev

.PARAMETER Location
    The Azure region for deployment. Default: westeurope

.PARAMETER WhatIf
    Perform a validation-only deployment without creating resources.

.EXAMPLE
    .\deploy.ps1 -Environment dev
    Deploy to development environment

.EXAMPLE
    .\deploy.ps1 -Environment prod -WhatIf
    Validate production deployment without creating resources
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment = 'dev',

    [Parameter()]
    [string]$Location = 'westeurope',

    [Parameter()]
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

# Colors for output
function Write-Info { Write-Host $args -ForegroundColor Cyan }
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }
function Write-Error { Write-Host $args -ForegroundColor Red }

Write-Info "╔══════════════════════════════════════════════════════════╗"
Write-Info "║  SobeSobe Infrastructure Deployment                      ║"
Write-Info "╚══════════════════════════════════════════════════════════╝"
Write-Info ""

# Check if Azure CLI is installed
Write-Info "Checking prerequisites..."
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed. Please install it from https://aka.ms/azure-cli"
    exit 1
}

# Check if logged in to Azure
Write-Info "Checking Azure login status..."
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Warning "Not logged in to Azure. Running 'az login'..."
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to login to Azure"
        exit 1
    }
    $account = az account show | ConvertFrom-Json
}

Write-Success "✓ Logged in as: $($account.user.name)"
Write-Success "✓ Subscription: $($account.name) ($($account.id))"
Write-Info ""

# Set deployment name
$deploymentName = "sobesobe-$Environment-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$parametersFile = Join-Path $PSScriptRoot "parameters/$Environment.parameters.json"
$templateFile = Join-Path $PSScriptRoot "main.bicep"

Write-Info "Deployment configuration:"
Write-Info "  Environment:    $Environment"
Write-Info "  Location:       $Location"
Write-Info "  Template:       $templateFile"
Write-Info "  Parameters:     $parametersFile"
Write-Info "  Deployment:     $deploymentName"
Write-Info ""

# Validate Bicep template
Write-Info "Validating Bicep template..."
az deployment sub validate `
    --location $Location `
    --template-file $templateFile `
    --parameters $parametersFile `
    --name "$deploymentName-validate" `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Template validation failed"
    exit 1
}
Write-Success "✓ Template validation passed"
Write-Info ""

if ($WhatIf) {
    Write-Warning "WhatIf mode: Performing what-if analysis..."
    az deployment sub what-if `
        --location $Location `
        --template-file $templateFile `
        --parameters $parametersFile `
        --name $deploymentName
    
    Write-Info ""
    Write-Success "✓ What-if analysis complete (no resources created)"
    exit 0
}

# Deploy infrastructure
Write-Info "Deploying infrastructure to Azure..."
Write-Info "(This may take 5-10 minutes)"
Write-Info ""

$deployment = az deployment sub create `
    --location $Location `
    --template-file $templateFile `
    --parameters $parametersFile `
    --name $deploymentName `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed"
    exit 1
}

Write-Success "✓ Deployment completed successfully"
Write-Info ""

# Display outputs
Write-Info "╔══════════════════════════════════════════════════════════╗"
Write-Info "║  Deployment Outputs                                      ║"
Write-Info "╚══════════════════════════════════════════════════════════╝"
Write-Info ""

$outputs = $deployment.properties.outputs
Write-Info "Resource Group:        $($outputs.resourceGroupName.value)"
Write-Info "App Service Name:      $($outputs.appServiceName.value)"
Write-Info "App Service URL:       $($outputs.appServiceUrl.value)"
Write-Info "Static Web App Name:   $($outputs.staticWebAppName.value)"
Write-Info "Static Web App URL:    $($outputs.staticWebAppUrl.value)"
Write-Info "Key Vault Name:        $($outputs.keyVaultName.value)"
Write-Info "App Insights Name:     $($outputs.applicationInsightsName.value)"
Write-Info "Database Server:       $($outputs.databaseServerName.value)"
Write-Info ""

# Save outputs to file
$outputsFile = Join-Path $PSScriptRoot "outputs/$Environment-outputs.json"
$outputsDir = Split-Path $outputsFile -Parent
if (-not (Test-Path $outputsDir)) {
    New-Item -ItemType Directory -Path $outputsDir -Force | Out-Null
}
$outputs | ConvertTo-Json -Depth 10 | Set-Content $outputsFile
Write-Success "✓ Outputs saved to: $outputsFile"
Write-Info ""

Write-Success "╔══════════════════════════════════════════════════════════╗"
Write-Success "║  Deployment Complete!                                    ║"
Write-Success "╚══════════════════════════════════════════════════════════╝"
Write-Info ""
Write-Info "Next steps:"
Write-Info "  1. Configure GitHub secrets for deployment (see docs/ci-cd.md)"
Write-Info "  2. Deploy application code via GitHub Actions"
Write-Info "  3. Configure custom domain and SSL (if needed)"
Write-Info ""
