using DotNet.Testcontainers.Builders;
using FuseDrill;
using System.Diagnostics;

namespace tests;

public class ApiIntegrationTests
{
    [Fact]
    public async Task TestApiContainer()
    {
        string imageTag = await PublishContainer();

        var dockerImageUrl = imageTag; // Change this to your actual image URL
        var containerName = "testapi";
        var apiBaseUrl = "http://localhost:8080/"; // Ensure this reflects the exposed port correctly
        var openApiSwaggerUrl = "http://localhost:8080/swagger/v1/swagger.json"; // Ensure this reflects the exposed port correctly

        // Set up a TestContainer for the image
        var containerBuilder = new ContainerBuilder()
            .WithImage(dockerImageUrl)
            .WithName(containerName)
            .WithPortBinding(8080, 8080) // Host:Container port mapping (Exposing port 8080)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
        //.WithWaitStrategy(Wait.ForUnixContainer().UntilContainerIsHealthy()); // Does not work,not sure why
        //https://devblogs.microsoft.com/dotnet/announcing-dotnet-chiseled-containers/
        //https://chatgpt.com/c/672fb9ac-73b0-8009-b163-46f1b5aeb12f

        var container = containerBuilder.Build();
        await container.StartAsync();

        try
        {
            await Task.Delay(1000);
            using var httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
            var fuzzer = new ApiFuzzerWithVerifier(httpClient, openApiSwaggerUrl);
            await fuzzer.TestWholeApi();
        }
        finally
        {
            // Stop and remove the container after the test
            await container.StopAsync();
            await container.DisposeAsync();
        }
    }

    static async Task<string> PublishContainer()
    {
        // build local fusedrill/testapi:latest container 
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var testApiDir = Path.Combine(repoRoot, "tests", "TestApi");
        var imageTag = "fusedrill/testapi:latest";

        // 1) dotnet publish the TestApi
        var publishInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "publish /t:PublishContainer -c Release --runtime linux-x64",
            WorkingDirectory = testApiDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var publishProc = Process.Start(publishInfo)!;
        var publishOut = await publishProc.StandardOutput.ReadToEndAsync();
        var publishErr = await publishProc.StandardError.ReadToEndAsync();
        publishProc.WaitForExit();
        if (publishProc.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet publish failed:\n{publishOut}\n{publishErr}");
        }

        return imageTag;
    }

}
