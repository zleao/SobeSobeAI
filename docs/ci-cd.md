# CI/CD Pipeline Documentation

This document describes the Continuous Integration and Continuous Deployment (CI/CD) setup for the SobeSobe project using GitHub Actions.

## Overview

The CI/CD pipeline consists of three workflows:
1. **Frontend CI** - Build, lint, test Angular frontend
2. **Backend CI** - Build, test .NET backend
3. **Deploy to Azure** - Deploy both frontend and backend to Azure

## Workflows

### Frontend CI (`frontend-ci.yml`)

**Triggers:**
- Push to `main` or `develop` branches with changes in `frontend/`
- Pull requests to `main` or `develop` with changes in `frontend/`

**Steps:**
1. Checkout code
2. Setup Node.js 20 with npm cache
3. Install dependencies (`npm ci`)
4. Run ESLint (`npm run lint`)
5. Run Prettier check
6. Build Angular application
7. Run unit tests with coverage (`npm run test:ci`)
8. Upload coverage to Codecov
9. Build production bundle
10. Upload build artifacts (retained for 7 days)

**Requirements:**
- Node.js 20
- npm 10+

---

### Backend CI (`backend-ci.yml`)

**Triggers:**
- Push to `main` or `develop` branches with changes in `backend/`
- Pull requests to `main` or `develop` with changes in `backend/`

**Steps:**
1. Checkout code
2. Setup .NET 10 SDK
3. Restore NuGet packages
4. Run .NET format check (`dotnet format --verify-no-changes`)
5. Build solution in Release configuration
6. Run unit tests with code coverage
7. Generate code coverage report
8. Upload coverage to Codecov
9. Publish API project
10. Upload publish artifacts (retained for 7 days)

**Requirements:**
- .NET 10 SDK

---

### Deploy to Azure (`deploy-azure.yml`)

**Triggers:**
- Push to `main` branch
- Manual workflow dispatch

**Jobs:**

#### 1. Build Frontend
- Builds production-optimized Angular bundle
- Uploads artifact for deployment

#### 2. Build Backend
- Builds and tests .NET solution
- Publishes API in Release configuration
- Uploads artifact for deployment

#### 3. Deploy Frontend
- Downloads frontend artifact
- Deploys to Azure Static Web Apps
- **Requires:** `AZURE_STATIC_WEB_APPS_API_TOKEN` secret

#### 4. Deploy Backend
- Downloads backend artifact
- Logs into Azure using service principal
- Deploys to Azure App Service
- **Requires:** `AZURE_CREDENTIALS` secret

#### 5. Smoke Tests
- Checks frontend health endpoint
- Checks backend health endpoint (`/health`)
- Notifies on failure

---

## GitHub Secrets

The following secrets must be configured in GitHub repository settings:

### Required Secrets

1. **`AZURE_STATIC_WEB_APPS_API_TOKEN`**
   - Token for deploying to Azure Static Web Apps
   - Obtained from Azure Portal → Static Web App → Deployment tokens

2. **`AZURE_CREDENTIALS`**
   - Service principal credentials for Azure authentication
   - Format:
     ```json
     {
       "clientId": "<client-id>",
       "clientSecret": "<client-secret>",
       "subscriptionId": "<subscription-id>",
       "tenantId": "<tenant-id>"
     }
     ```
   - Create service principal:
     ```bash
     az ad sp create-for-rbac --name "sobesobe-github-actions" \
       --role contributor \
       --scopes /subscriptions/<subscription-id>/resourceGroups/rg-sobesobe-prod \
       --sdk-auth
     ```

### Optional Secrets

1. **`CODECOV_TOKEN`**
   - Token for uploading code coverage to Codecov
   - Optional if repository is public

---

## Branch Protection Rules

Configure branch protection for `main` branch in GitHub:

### Settings
1. Go to: **Settings** → **Branches** → **Branch protection rules**
2. Click **Add rule**
3. Branch name pattern: `main`
4. Enable:
   - ☑ Require a pull request before merging
   - ☑ Require approvals (1 approval)
   - ☑ Require status checks to pass before merging
     - Required checks:
       - `frontend-build-test`
       - `backend-build-test`
   - ☑ Require branches to be up to date before merging
   - ☑ Require linear history
   - ☑ Do not allow bypassing the above settings

---

## Code Quality Checks

### Frontend
- **ESLint**: Lints TypeScript and Angular code
- **Prettier**: Checks code formatting
- **Unit Tests**: Runs with Karma/Jasmine
- **Coverage**: Minimum 80% coverage recommended

### Backend
- **dotnet format**: Enforces C# code style
- **.NET Analyzers**: Static code analysis
- **Unit Tests**: xUnit/NUnit
- **Coverage**: Minimum 80% coverage recommended

---

## Artifacts

Both CI workflows produce artifacts that are retained for 7 days:

### Frontend Artifacts
- **`frontend-dist`**: Built Angular application
- **Location**: `frontend/dist`
- **Use**: Can be downloaded for local testing or manual deployment

### Backend Artifacts
- **`backend-publish`**: Published .NET API
- **Location**: `backend/publish`
- **Use**: Can be deployed manually to any .NET 10 host

---

## Local Testing

### Test Frontend CI Locally
```bash
cd frontend
npm ci
npm run lint
npm run test:ci
npm run build -- --configuration production
```

### Test Backend CI Locally
```bash
cd backend
dotnet restore
dotnet format --verify-no-changes
dotnet build --configuration Release
dotnet test --configuration Release --collect:"XPlat Code Coverage"
dotnet publish SobeSobe.Api/SobeSobe.Api.csproj --configuration Release
```

---

## Troubleshooting

### Frontend CI Issues

**Issue:** ESLint or Prettier failures
- **Fix:** Run `npm run lint` locally and fix reported issues
- **Fix:** Run `npx prettier --write "src/**/*.{ts,html,css}"` to auto-format

**Issue:** Unit tests failing
- **Fix:** Run `npm run test` locally to debug failures
- **Fix:** Ensure all tests pass before pushing

**Issue:** Build failures
- **Fix:** Run `npm run build` locally to reproduce
- **Fix:** Check for missing dependencies or TypeScript errors

### Backend CI Issues

**Issue:** dotnet format failures
- **Fix:** Run `dotnet format` locally to auto-format code
- **Fix:** Configure IDE to use EditorConfig for consistent formatting

**Issue:** Build failures
- **Fix:** Run `dotnet build` locally to reproduce
- **Fix:** Ensure all NuGet packages are restored

**Issue:** Test failures
- **Fix:** Run `dotnet test` locally to debug
- **Fix:** Check for environment-specific dependencies (e.g., database connections)

### Deployment Issues

**Issue:** Azure Static Web Apps deployment fails
- **Fix:** Verify `AZURE_STATIC_WEB_APPS_API_TOKEN` is correct
- **Fix:** Check Azure Static Web App configuration in portal

**Issue:** Azure App Service deployment fails
- **Fix:** Verify `AZURE_CREDENTIALS` secret is valid
- **Fix:** Ensure App Service exists and is running
- **Fix:** Check Azure portal for deployment logs

**Issue:** Smoke tests fail
- **Fix:** Check application logs in Azure portal
- **Fix:** Verify health endpoints are responding correctly
- **Fix:** Test endpoints manually with `curl`

---

## Future Enhancements

- [ ] Add integration tests to CI pipeline
- [ ] Add end-to-end (E2E) tests with Playwright
- [ ] Add performance testing
- [ ] Add security scanning (Dependabot, CodeQL)
- [ ] Add automatic semantic versioning
- [ ] Add automated changelog generation
- [ ] Add notification system (Slack, Discord) for deployment status
- [ ] Add staging environment deployment
- [ ] Add blue-green or canary deployment strategy
- [ ] Add automated rollback on smoke test failure

---

## Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Azure Static Web Apps CI/CD](https://learn.microsoft.com/en-us/azure/static-web-apps/github-actions-workflow)
- [Azure App Service Deployment](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions)
- [Codecov GitHub Action](https://github.com/codecov/codecov-action)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/)
- [Angular Testing Guide](https://angular.dev/guide/testing)
