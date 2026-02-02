# Using .NET Aspire for Local Development

## Overview

.NET Aspire is used to orchestrate the SobeSobe application's services during local development. It provides:

- Service orchestration for backend API
- Health checks and service monitoring
- Telemetry and observability via Aspire Dashboard
- Simplified multi-service startup

## Prerequisites

No additional workloads required! .NET Aspire 9.5.2 works with .NET 10 SDK via NuGet packages only.

Requirements:
- .NET 10 SDK (already installed)
- Node.js and npm (for frontend development)

## Running the Application

### Option 1: Using Visual Studio 2022 (17.9+)

1. Open `backend/SobeSobe.sln`
2. Set `SobeSobe.AppHost` as the startup project
3. Press F5 or click Run
4. The Aspire Dashboard will open automatically in your browser

### Option 2: Using Command Line

1. Build the API first:
   ```bash
   cd backend
   dotnet build SobeSobe.Api/SobeSobe.Api.csproj
   ```

2. Run the AppHost:
   ```bash
   dotnet run --project SobeSobe.AppHost
   ```

3. The Aspire Dashboard will be available at `https://localhost:17032` (HTTPS) or `http://localhost:15283` (HTTP)

### Option 3: Manual Service Startup (Without Aspire)

1. Start the backend API:
   ```bash
   cd backend/SobeSobe.Api
   dotnet run
   ```

2. In a separate terminal, start the frontend:
   ```bash
   cd frontend
   npm start
   ```

API will run on https://localhost:5001, frontend on http://localhost:4200.

## Aspire Dashboard

The Aspire Dashboard provides:

- **Resources**: View all running services (API, frontend, database)
- **Console Logs**: See logs from all services in real-time
- **Traces**: Distributed tracing across services
- **Metrics**: Performance metrics and health checks
- **Structured Logs**: Search and filter application logs

Access the dashboard at: `https://localhost:17032` (default HTTPS port)

## Services

### Backend API (`api`)
- Runs on https://localhost:5001 (HTTPS) or http://localhost:5000 (HTTP)
- Accessible via Aspire service orchestration
- Health checks at `/health` and `/alive` (via ServiceDefaults)
- OpenAPI docs at `/openapi/v1.json`
- Sample endpoint: `/weatherforecast`

### Frontend (Manual startup for now)
- Run separately: `cd frontend && npm start`
- Runs on http://localhost:4200
- Will be integrated with AppHost in future iterations

### Database
SQLite database configuration will be added when Entity Framework Core is set up (Issue #16).
For now, database setup is deferred until schema design is complete.

## Service Discovery

Service discovery will be configured when frontend integration is added.

For now:
- Frontend: http://localhost:4200 (manual startup)
- Backend API: https://localhost:5001 (via AppHost or manual)

## Environment Variables

Environment variables are automatically set by Aspire ServiceDefaults:

### API Service
- `ASPNETCORE_ENVIRONMENT`: Development
- `OTEL_EXPORTER_OTLP_ENDPOINT`: Telemetry endpoint for Aspire Dashboard (when running via AppHost)
- Connection strings and other configurations via appsettings.json

## Health Checks

Health checks are provided by Aspire ServiceDefaults:

- **API**: `/health` (overall health), `/alive` (liveness check)
- Automatic health check reporting to Aspire Dashboard

View health status in the Aspire Dashboard under "Resources".

## Debugging

### Debugging the API

1. In Visual Studio: Set breakpoints in `SobeSobe.Api` and run AppHost
2. In VS Code: Attach to the `SobeSobe.Api` process after starting AppHost

### Debugging the Frontend

1. Start AppHost to run all services
2. Frontend runs with `ng serve` on port 4200
3. Use browser DevTools or VS Code debugger

### Viewing Logs

- All service logs are visible in the Aspire Dashboard
- Console logs show in real-time
- Structured logs can be filtered by service, level, and message

## Stopping the Application

Press `Ctrl+C` in the terminal where AppHost is running, or stop debugging in Visual Studio.

All services will be gracefully shut down.

## Troubleshooting

### Port Already in Use

If ports are in use, Aspire will assign different ports. Check the Dashboard for actual URLs.

### Frontend Not Starting

Ensure Node.js and npm are installed:
```bash
node --version
npm --version
```

Frontend is currently started manually. AppHost integration will be added in a future iteration.

### Database Connection Issues

Database setup is deferred until Issue #16 (database schema design) is complete.
For now, the API runs without a database connection.

### Aspire Dashboard Not Opening

If the dashboard doesn't open automatically:
- HTTPS: `https://localhost:17032`
- HTTP: `http://localhost:15283`

Check launchSettings.json for configured ports.

## Production Deployment

Aspire is for **local development only**. For production:

- Frontend: Deploy to Azure Static Web Apps
- Backend: Deploy to Azure App Service
- Database: Migrate to Azure SQL Database or keep SQLite

See `infrastructure/` for Bicep deployment templates.

## Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Aspire Hosting](https://learn.microsoft.com/dotnet/aspire/fundamentals/app-host-overview)
- [Service Discovery](https://learn.microsoft.com/dotnet/aspire/service-discovery/overview)
