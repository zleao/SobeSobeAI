var builder = DistributedApplication.CreateBuilder(args);

// Add backend API - using AddExecutable as workaround for project reference issues
var apiPath = Path.Combine(builder.AppHostDirectory, "..", "SobeSobe.Api", "bin", "Debug", "net10.0", "SobeSobe.Api.dll");
var api = builder.AddExecutable("api", "dotnet", builder.AppHostDirectory, apiPath)
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithHttpsEndpoint(port: 5001, name: "https");

builder.Build().Run();
