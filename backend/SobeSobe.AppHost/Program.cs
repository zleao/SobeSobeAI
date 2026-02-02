var builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> backend = builder
    .AddProject<Projects.SobeSobe_Api>("backend");

builder.AddJavaScriptApp("frontend", "../../frontend")
    .WithRunScript("start")
    .WithReference(backend)
    .WaitFor(backend)
    .WithHttpEndpoint(env: "PORT", port: 4500)
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
