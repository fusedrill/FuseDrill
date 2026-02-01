var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.RequestCaptureAndReplay_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
