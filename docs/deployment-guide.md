# SobeSobe Deployment Guide

## Current Status

✅ **Infrastructure as Code**: Complete  
✅ **CI/CD Pipelines**: Configured  
✅ **Application Code**: Production-ready  
⏳ **Azure Deployment**: Pending (requires Azure subscription)

## Overview

This guide walks through deploying the SobeSobe application to Azure. All infrastructure code and deployment pipelines are ready. The only requirement is an active Azure subscription with appropriate permissions.

## Prerequisites

### Required Tools
- Azure CLI (`az`) - [Install](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- PowerShell 7+ - [Install](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell)
- GitHub CLI (`gh`) - [Install](https://cli.github.com/)

### Required Permissions
- **Azure**: Subscription Owner or Contributor role
- **GitHub**: Repository admin access (for secrets configuration)

### Required Information
- Azure Subscription ID
- GitHub repository: `zleao/SobeSobeAI`
- Custom domain names (optional, for production)

## Step-by-Step Deployment

### Step 1: Azure Login

```powershell
# Login to Azure
az login

# Select your subscription
az account set --subscription "<your-subscription-id>"

# Verify selected subscription
az account show
```

### Step 2: Deploy Infrastructure (Development)

Navigate to the infrastructure directory and run the deployment script:

```powershell
cd infrastructure

# Run what-if to preview changes (recommended first)
.\deploy.ps1 -Environment dev -WhatIf

# Deploy infrastructure
.\deploy.ps1 -Environment dev
```

**Expected Output:**
- Resource group `rg-sobesobe-dev` created
- App Service Plan and App Service created
- Azure Static Web App created
- Key Vault created
- Application Insights and Log Analytics workspace created
- SQLite database configured (no Azure SQL for dev)
- Deployment outputs saved to `outputs/dev-outputs.json`

**Estimated Time**: 5-10 minutes

**Estimated Cost**: ~$15-20/month

### Step 3: Configure GitHub Secrets

After successful deployment, configure GitHub secrets using the outputs:

```powershell
# Read deployment outputs
$outputs = Get-Content outputs/dev-outputs.json | ConvertFrom-Json

# Configure GitHub secrets
gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --body $outputs.staticWebAppToken.value
gh secret set AZURE_APP_SERVICE_NAME --body $outputs.appServiceName.value
gh secret set AZURE_APP_SERVICE_PUBLISH_PROFILE --body (az webapp deployment list-publishing-profiles --name $outputs.appServiceName.value --resource-group rg-sobesobe-dev --xml)
gh secret set CODECOV_TOKEN --body "<your-codecov-token>"

# Verify secrets
gh secret list
```

### Step 4: Trigger Deployment Pipeline

Push a commit to the `main` branch to trigger the automated deployment:

```powershell
# Make a small change (or use --allow-empty)
git commit --allow-empty -m "chore: trigger initial deployment"
git push origin main
```

**Expected Workflow:**
1. Frontend CI runs: builds, tests, lints frontend
2. Backend CI runs: builds, tests backend
3. Deploy to Azure runs: deploys frontend to Static Web Apps, backend to App Service
4. Smoke tests run: verifies health endpoints

**Monitoring**: Watch the deployment at `https://github.com/zleao/SobeSobeAI/actions`

### Step 5: Verify Deployment

After the pipeline completes:

```powershell
# Get frontend URL
$frontendUrl = az staticwebapp show --name $outputs.staticWebAppName.value --resource-group rg-sobesobe-dev --query "defaultHostname" -o tsv
Write-Host "Frontend: https://$frontendUrl"

# Get backend URL
$backendUrl = az webapp show --name $outputs.appServiceName.value --resource-group rg-sobesobe-dev --query "defaultHostName" -o tsv
Write-Host "Backend: https://$backendUrl"

# Test health endpoints
curl "https://$backendUrl/health"
curl "https://$backendUrl/alive"
```

**Expected Results:**
- Frontend loads successfully at Static Web App URL
- Backend health endpoints return 200 OK
- Application Insights shows telemetry data
- Key Vault accessible via managed identity

## Environment-Specific Deployment

### Staging Environment

```powershell
# Deploy staging infrastructure
.\deploy.ps1 -Environment staging

# Configure GitHub secrets for staging
# ... (repeat Step 3 with staging outputs)
```

**Estimated Cost**: ~$70-75/month

### Production Environment

```powershell
# Deploy production infrastructure
.\deploy.ps1 -Environment prod

# Configure GitHub secrets for production
# ... (repeat Step 3 with prod outputs)
```

**Estimated Cost**: ~$355-365/month

**Production-Specific Steps:**
1. Configure custom domain for Static Web App
2. Configure custom domain for App Service
3. Set up SSL certificates (automatic with Azure-managed certificates)
4. Configure backup and disaster recovery
5. Set up monitoring alerts in Application Insights
6. Enable Azure SQL Database (included in production Bicep template)

## Post-Deployment Configuration

### 1. Application Insights Alerts

Configure monitoring alerts for:
- Failed requests (>5% error rate)
- Slow response times (>2s average)
- High CPU/Memory usage (>80%)

```powershell
# Example: Create alert for high error rate
az monitor metrics alert create --name "High Error Rate" \
  --resource-group rg-sobesobe-prod \
  --scopes "/subscriptions/<sub-id>/resourceGroups/rg-sobesobe-prod/providers/Microsoft.Insights/components/<app-insights-name>" \
  --condition "avg failed_requests > 50" \
  --window-size 5m \
  --evaluation-frequency 1m
```

### 2. Backup Strategy

For production Azure SQL Database:

```powershell
# Configure automated backups (already enabled by default)
# Point-in-time restore available for 7-35 days

# Test backup restore
az sql db restore --dest-name sobesobe-db-restore \
  --server <sql-server-name> \
  --resource-group rg-sobesobe-prod \
  --name sobesobe-db \
  --time "2026-02-03T12:00:00Z"
```

### 3. Cost Optimization

Monitor and optimize costs:

```powershell
# View current costs
az consumption usage list --start-date 2026-02-01 --end-date 2026-02-03

# Set up budget alerts
az consumption budget create --budget-name "SobeSobe Monthly Budget" \
  --amount 400 \
  --category Cost \
  --time-grain Monthly \
  --start-date 2026-02-01 \
  --end-date 2027-02-01
```

### 4. Scaling Configuration

Adjust scaling settings based on traffic:

```powershell
# Configure App Service autoscaling
az monitor autoscale create --resource-group rg-sobesobe-prod \
  --resource <app-service-id> \
  --min-count 1 \
  --max-count 5 \
  --count 2

# Add CPU-based scaling rule
az monitor autoscale rule create --resource-group rg-sobesobe-prod \
  --autoscale-name <autoscale-name> \
  --condition "Percentage CPU > 70 avg 5m" \
  --scale out 1
```

## Troubleshooting

### Issue: Deployment Script Fails

**Symptoms**: `deploy.ps1` exits with errors

**Solutions**:
1. Verify Azure CLI is installed and logged in: `az account show`
2. Check subscription permissions: `az role assignment list --assignee <your-email>`
3. Review error messages in PowerShell output
4. Try what-if first: `.\deploy.ps1 -Environment dev -WhatIf`

### Issue: GitHub Actions Deployment Fails

**Symptoms**: Deploy workflow fails in GitHub Actions

**Solutions**:
1. Verify GitHub secrets are configured: `gh secret list`
2. Check Azure credentials haven't expired
3. Review workflow logs: `gh run view --log`
4. Verify App Service and Static Web App exist in Azure

### Issue: Application Not Accessible

**Symptoms**: 404 or 503 errors when accessing URLs

**Solutions**:
1. Check deployment logs in Azure Portal
2. Verify App Service is running: `az webapp show --name <app-name> --query "state"`
3. Check Application Insights for errors
4. Restart App Service: `az webapp restart --name <app-name> --resource-group rg-sobesobe-dev`

### Issue: Database Connection Errors

**Symptoms**: API returns 500 errors, database connection failures

**Solutions**:
1. Verify connection string in Key Vault
2. Check App Service has managed identity assigned
3. Verify Key Vault access policy includes App Service identity
4. For Azure SQL: Check firewall rules allow Azure services

### Issue: High Costs

**Symptoms**: Azure costs exceed estimates

**Solutions**:
1. Review Azure Cost Management: `az consumption usage list`
2. Scale down non-production environments
3. Use SQLite for dev/staging (already configured)
4. Set up budget alerts (see Post-Deployment Configuration)

## Rollback Procedure

If a deployment causes issues:

### Option 1: Redeploy Previous Version

```powershell
# Find previous successful deployment commit
git log --oneline

# Checkout previous commit
git checkout <previous-commit-sha>

# Push to main to trigger redeployment
git push origin HEAD:main --force
```

### Option 2: Manual Rollback in Azure

```powershell
# Swap deployment slots (if configured)
az webapp deployment slot swap --name <app-name> \
  --resource-group rg-sobesobe-prod \
  --slot staging \
  --target-slot production
```

## Security Considerations

✅ **Implemented**:
- HTTPS enforcement (TLS 1.2 minimum)
- Azure Key Vault for secrets
- Managed identity for authentication
- RBAC authorization on Key Vault
- Soft delete and purge protection
- Network security (Azure services only for SQL)

📋 **Recommended**:
- Enable Azure DDoS Protection (production)
- Configure Web Application Firewall
- Set up Azure Security Center alerts
- Regular security audits with Azure Advisor
- Implement IP restrictions on App Service (if needed)

## Monitoring and Observability

### Application Insights Queries

Common queries for monitoring:

```kusto
// Failed requests
requests
| where success == false
| summarize count() by resultCode, bin(timestamp, 1h)

// Slow requests
requests
| where duration > 2000
| project timestamp, name, duration, url

// Exception trends
exceptions
| summarize count() by type, bin(timestamp, 1h)
```

### Log Analytics Dashboards

Access dashboards at:
- Azure Portal → Application Insights → Workbooks
- Custom dashboards with KPIs: response time, error rate, user count

### Aspire Dashboard (Local Development)

For local development monitoring:

```powershell
cd backend/SobeSobe.AppHost
dotnet run
```

Access at: `https://localhost:17032` or `http://localhost:15283`

## Next Steps

1. ✅ **Infrastructure**: Complete
2. ✅ **CI/CD**: Complete
3. ⏳ **Deployment**: Execute Steps 1-5 above
4. ⏳ **SSL Certificates**: Configure custom domains (production)
5. ⏳ **Monitoring**: Set up Application Insights alerts
6. ⏳ **Backup**: Test disaster recovery procedures
7. ⏳ **Performance**: Load testing and optimization
8. ⏳ **Documentation**: Update with production URLs

## Support and Resources

- **Infrastructure Code**: `infrastructure/` directory
- **CI/CD Configuration**: `.github/workflows/`
- **Documentation**: `docs/` directory
- **Azure Portal**: https://portal.azure.com
- **GitHub Repository**: https://github.com/zleao/SobeSobeAI

---

**Last Updated**: 2026-02-03  
**Status**: Infrastructure Ready, Awaiting Azure Deployment
