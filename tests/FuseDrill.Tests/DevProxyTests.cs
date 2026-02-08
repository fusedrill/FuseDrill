using FuseDrill.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Tests;

public sealed class DevProxyTests
{
    [Fact]
    public async Task CaptureAndReplayExternalCall()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"dev-proxy-{Guid.NewGuid():N}.json");

        try
        {
            var capturedResponse = await CallExternalEndpointAsync(
                new DevProxyOptions
                {
                    Mode = DevProxyMode.Capture,
                    StoragePath = storagePath,
                    InternalHosts = new[] { "localhost" }
                });

            Assert.True(File.Exists(storagePath));
            Assert.False(string.IsNullOrWhiteSpace(capturedResponse));

            var replayedResponse = await CallExternalEndpointAsync(
                new DevProxyOptions
                {
                    Mode = DevProxyMode.Replay,
                    StoragePath = storagePath,
                    InternalHosts = new[] { "localhost" }
                },
                new ThrowingHandler());

            Assert.Equal(capturedResponse, replayedResponse);
        }
        finally
        {
            if (File.Exists(storagePath))
            {
                File.Delete(storagePath);
            }
        }
    }

    private static async Task<string> CallExternalEndpointAsync(DevProxyOptions options, HttpMessageHandler? innerHandler = null)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddHttpClient("external")
                        .ConfigurePrimaryHttpMessageHandler(() => new DevProxyHandler(options, innerHandler));
                });
            });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/WeatherForecast/external");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("External call attempted during replay.");
        }
    }
}
