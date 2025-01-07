using DotNet.Testcontainers.Builders;
using FuseDrill;

namespace tests;
public class ApiIntegrationTests
{   
    [Fact]
    public async Task TestApiContainer()
    {
        var dockerImageUrl = "fusedrill/testapi:latest"; // Change this to your actual image URL
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
}
