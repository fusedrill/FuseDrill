using FuseDrill;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Testcontainers.Core.Builders;
using Testcontainers.Core.Containers;
using Xunit;

namespace Tests
{
    public class PetsApiTests
    {        [Fact]
        public async Task TestPetsEndpoint()
        {
            // Setup Docker container
            using var testContainer = await new TestContainerBuilder()
                .WithImage("ghcr.io/fusedrill/fusedrill/testapi:latest")
                .WithExposedPort(8080)
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                .BuildAndStartAsync();

            var port = testContainer.GetMappedPort(8080);
            var baseUrl = $"http://localhost:{port}";
            var openApiUrl = $"{baseUrl}/swagger/v1/swagger.json";
            
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };

            // Create fuzzer instance
            var fuzzer = new ApiFuzzer(httpClient, openApiUrl);

            // Run fuzzing tests specifically for Pets endpoint
            var results = await fuzzer.TestWholeApi(apiCall =>
            {
                var filterEndpoint = apiCall.MethodName.EndsWith("/Pets");
                
                if (filterEndpoint)
                {
                    // Customize test parameters for Pets endpoint
                    apiCall.RequestParameters = new[]
                    {
                        new KeyValuePair<string, object>("breed", "Labrador"),
                        new KeyValuePair<string, object>("name", "Max"),
                        new KeyValuePair<string, object>("petType", 0)
                    };
                }

                return filterEndpoint;
            });

            // Assert
            Assert.NotNull(results);
            Assert.NotEmpty(results.TestSuites);
        }
    }
}
