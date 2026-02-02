# SobeSobe Infrastructure

This directory contains Azure infrastructure as code (IaC) using Bicep templates.

## Overview

The infrastructure is organized into:
- **main.bicep**: Main orchestration template
- **modules/**: Reusable Bicep modules for each resource type
- **parameters/**: Environment-specific parameter files (dev, staging, prod)
- **deploy.ps1**: Deployment script with validation and what-if support

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Resource Group: rg-sobesobe-{env}                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌────────────────┐  ┌──────────────────┐                  │
│  │ Static Web App │  │  App Service     │                  │
│  │  (Frontend)    │──│  (Backend API)   │                  │
│  └────────────────┘  └──────────────────┘                  │
│          │                    │                            │
│          │                    │                            │
│          ▼                    ▼                            │
│  ┌────────────────────────────────┐                        │
│  │   Application Insights         │                        │
│  │   (Monitoring & Logging)       │                        │
│  └────────────────────────────────┘                        │
│                    │                                       │
│                    ▼                                       │
│  ┌────────────────────────────────┐                        │
│  │      Key Vault                 │                        │
│  │   (Secrets & Config)           │                        │
│  └────────────────────────────────┘                        │
│                                                             │
│  ┌────────────────────────────────┐ (prod only)            │
│  │    Azure SQL Database          │                        │
│  │   (Game Data Storage)          │                        │
│  └────────────────────────────────┘                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Resources

### Development Environment
- **App Service Plan**: B1 (Basic, 1 instance) - ~$13/month
- **Static Web App**: Free tier - $0
- **Application Insights**: 5GB free tier - $0-5/month
- **Key Vault**: Standard - ~$1/month
- **Database**: SQLite (local) - $0
- **Total**: ~$15-20/month

### Staging Environment
- **App Service Plan**: B2 (Basic, 1 instance) - ~$54/month
- **Static Web App**: Standard - $9/month
- **Application Insights**: Pay-as-you-go - ~$5-10/month
- **Key Vault**: Standard - ~$1/month
- **Database**: SQLite (local) - $0
- **Total**: ~$70-75/month

### Production Environment
- **App Service Plan**: P1v3 (Premium, 2 instances) - ~$328/month
- **Static Web App**: Standard - $9/month
- **Azure SQL Database**: Basic (2GB) - ~$5/month
- **Application Insights**: Pay-as-you-go - ~$10-20/month
- **Key Vault**: Standard - ~$1/month
- **Total**: ~$355-365/month

## Prerequisites

1. **Azure CLI**: [Install Azure CLI](https://aka.ms/azure-cli)
2. **Azure Subscription**: Active Azure subscription
3. **Permissions**: Contributor or Owner role on subscription

## Deployment

### Quick Start

```powershell
# Deploy to development
.\deploy.ps1 -Environment dev

# Deploy to production
.\deploy.ps1 -Environment prod

# Validate without deploying (what-if)
.\deploy.ps1 -Environment prod -WhatIf
```

### Step-by-Step Deployment

1. **Login to Azure**
   ```powershell
   az login
   az account set --subscription "Your Subscription Name"
   ```

2. **Validate Template**
   ```powershell
   .\deploy.ps1 -Environment dev -WhatIf
   ```

3. **Deploy Infrastructure**
   ```powershell
   .\deploy.ps1 -Environment dev
   ```

4. **Save Deployment Outputs**
   - Outputs are automatically saved to `outputs/{env}-outputs.json`
   - Use these values to configure GitHub secrets

### Manual Deployment (Azure CLI)

```powershell
# Validate
az deployment sub validate \
  --location westeurope \
  --template-file main.bicep \
  --parameters parameters/dev.parameters.json

# Deploy
az deployment sub create \
  --location westeurope \
  --template-file main.bicep \
  --parameters parameters/dev.parameters.json \
  --name sobesobe-dev
```

## Configuration

### Environment Parameters

Edit `parameters/{env}.parameters.json` to customize:
- **environmentName**: dev, staging, or prod
- **location**: Azure region (default: westeurope)

### Resource Naming Convention

Resources are named using the pattern: `{type}-sobesobe-{suffix}`

Examples:
- `rg-sobesobe-dev` (Resource Group)
- `app-sobesobe-api-abc123` (App Service)
- `swa-sobesobe-abc123` (Static Web App)
- `kv-sobesobe-abc123` (Key Vault)

The suffix is auto-generated using `uniqueString()` to ensure global uniqueness.

## Modules

### app-service.bicep
- App Service Plan (Linux)
- App Service for .NET 10 backend
- Managed identity for Key Vault access
- Health check endpoint configuration
- HTTPS enforcement

### static-web-app.bicep
- Static Web App for Angular frontend
- GitHub integration for CI/CD
- Custom domain support (Standard SKU)
- Free tier for dev, Standard for staging/prod

### monitoring.bicep
- Log Analytics Workspace
- Application Insights
- 30-day retention (dev/staging), 90-day (prod)
- Integrated with App Service and Static Web App

### key-vault.bicep
- Azure Key Vault for secrets
- RBAC authorization enabled
- Soft delete enabled (90-day retention)
- Purge protection enabled (prod)

### database.bicep (production only)
- Azure SQL Server
- Azure SQL Database (Basic SKU)
- Firewall rules for Azure services
- Connection string stored in Key Vault

## Security

### Managed Identity
The App Service uses a system-assigned managed identity to access Key Vault. No passwords or secrets are stored in app settings.

### Key Vault RBAC
- App Service principal has "Key Vault Secrets User" role
- Allows reading secrets at runtime
- No direct access to keys or certificates

### Network Security
- HTTPS enforcement on all services
- TLS 1.2 minimum
- FTPS disabled on App Service
- Azure SQL accessible only from Azure services

## Monitoring

### Application Insights
- Real-time performance monitoring
- Request/response logging
- Exception tracking
- Custom metrics and events

### Log Analytics
- Centralized logging
- Query and analyze logs
- Alerts and notifications

## Database Strategy

### Development/Staging
- **SQLite**: File-based database for simplicity
- Fast iteration and testing
- No Azure SQL costs

### Production
- **Azure SQL Database**: Managed relational database
- Automatic backups and geo-replication
- Scalable and highly available

### Migration Path
1. Design schema in SQLite (dev/staging)
2. Test EF Core migrations locally
3. Deploy to Azure SQL (prod)
4. Run migrations in production

## Cost Optimization

### Development
- Use B1 App Service Plan (cheapest)
- Free tier Static Web App
- SQLite (no database costs)
- Minimal Application Insights usage

### Production
- P1v3 App Service Plan (performance + scale)
- Azure SQL Basic tier (upgrade as needed)
- Standard Static Web App (custom domains)
- Monitor costs with Azure Cost Management

## Troubleshooting

### Deployment Fails with "Name already exists"
Some resources require globally unique names (Key Vault, Static Web App). The `uniqueString()` function generates unique suffixes, but conflicts can occur. Re-run deployment or manually delete conflicting resources.

### App Service doesn't start
- Check Application Insights for errors
- Verify Key Vault access (managed identity configured)
- Check app settings (ASPNETCORE_ENVIRONMENT, KeyVaultName)

### Static Web App not deploying
- Verify GitHub repository URL in `static-web-app.bicep`
- Check GitHub Actions for build logs
- Ensure `outputLocation` matches Angular build output

### Key Vault access denied
- Verify managed identity has "Key Vault Secrets User" role
- Check Key Vault firewall rules
- Wait 5-10 minutes for RBAC propagation

## Next Steps

1. **Configure GitHub Secrets** (see [docs/ci-cd.md](../docs/ci-cd.md))
   - `AZURE_STATIC_WEB_APPS_API_TOKEN`
   - `AZURE_CREDENTIALS`
   
2. **Deploy Application Code**
   - Push to `main` branch triggers deployment
   - GitHub Actions builds and deploys automatically

3. **Configure Custom Domain** (Production)
   - Add custom domain to Static Web App
   - Configure SSL certificate
   - Update DNS records

4. **Setup Alerts and Monitoring**
   - Configure Application Insights alerts
   - Set up Azure Monitor dashboards
   - Enable autoscaling (if needed)

## References

- [Azure Bicep Documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [Azure App Service](https://learn.microsoft.com/azure/app-service/)
- [Azure Static Web Apps](https://learn.microsoft.com/azure/static-web-apps/)
- [Azure Key Vault](https://learn.microsoft.com/azure/key-vault/)
- [Application Insights](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)
