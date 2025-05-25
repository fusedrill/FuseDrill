using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using FuseDrill.Core;
using FuseDrill.MCP.Services;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Writers;
using ModelContextProtocol.Server;
using SharpYaml.Serialization;

namespace FuseDrill.MCP.Tools;

[McpServerToolType]
public static class FuseDrillTools
{
    private readonly static ApiAnalysisService _apiAnalysisService = new();

    [McpServerTool, Description("FirstPassFuzzing: Run first pass fuzzing in order to know about the API shape, You need to figure out first inputs of the API using curl or other tool, then you can use this tool to run first pass fuzzing on the API")]
    public static async Task<string> RunFirstPassFuzzing(
        [Description("(First figure out if server hostname and port if it is running using curl) Base URL for the API")] string baseUrl,
        [Description("(First figure out if server hostname and port if it is running using curl) OpenAPI URL for the API")] string openAPIUrl,
        [Description("(Optional) Ask user if the service requires authentication")] string? bearerToken)
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(baseUrl);

        //auth header
        if (bearerToken != null)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        var fuzzer = new ApiFuzzer(httpClient, openAPIUrl);
        var results = await fuzzer.TestWholeApi();

        // Return serialized test details
        return JsonSerializer.Serialize(new
        {
            BaseUrl = baseUrl,
            OpenAPIUrl = openAPIUrl,
            TestSuites = results.TestSuites,
            NextSteps = "Now you can use the results to understand the API shape and run specific endpoint fuzzing tests. But first you need to ask a user which endpoint specifically they want to test."
        });
    }

    [McpServerTool, Description("Now that you know the api shape from FirstPassFuzzing, you can run a specific ai enhanced test for single endpoint")]
    public static async Task<string> RunSpecificEndpointFuzzing(
        [Description("The API endpoint to test")] string endpoint,
        [Description("Base URL for the API")] string baseUrl,
        [Description("OpenAPI URL for the API")] string OpenAPIUrl,
        [Description("The API request parameters (This information you can get from FirstPassFuzzing)")] List<ParameterValue> RequestParameters)
    {
        var httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(baseUrl))
        {
            httpClient.BaseAddress = new Uri(baseUrl);
        }

        var fuzzer = new ApiFuzzer(httpClient, OpenAPIUrl);
        var results = await fuzzer.TestWholeApi(apiCall =>
        {
            var filterEndpoint = apiCall.MethodName.EndsWith(endpoint);

            //change body on specific endpoint
            if (filterEndpoint)
            {
                apiCall.RequestParameters = RequestParameters; // override filtered endpoint with request parameters
            }

            return filterEndpoint;
        });

        // Return serialized test details
        return JsonSerializer.Serialize(new
        {
            Endpoint = endpoint,
            BaseUrl = baseUrl,
            OpenAPIUrl = OpenAPIUrl,
            TestSuites = results.TestSuites,
            NextSteps = "You can now review the test results for the specific endpoint. Now you can ask user if it want to implement a remote fuzzing test for this endpoint. Use the tool GetCSharpTemplateCodeOfHowToImplementRemoteFuzzingTest to show how the test code will look like"
        });
    }

    [McpServerTool, Description("if user asks to write a a fuzzing test for specific endpoint, you can get a c sharp template code of how to implement remote fuzzing test for a specific endpoint")]
    public static async Task<string> GetCSharpTemplateCodeOfHowToImplementRemoteFuzzingTest()
    {
        var template = @"var httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(baseUrl))
        {
            httpClient.BaseAddress = new Uri(baseUrl);
        }

        var fuzzer = new ApiFuzzer(httpClient, OpenAPIUrl);
        var results = await fuzzer.TestWholeApi(apiCall =>
        {
            var filterEndpoint = apiCall.MethodName.EndsWith(endpoint);

            //change body on specific endpoint
            if (filterEndpoint)
            {
                apiCall.RequestParameters = RequestParameters;
            }

            return filterEndpoint;
        });

        // Return serialized test details
        return JsonSerializer.Serialize(new
        {
            Endpoint = endpoint,
            BaseUrl = baseUrl,
            OpenAPIUrl = OpenAPIUrl,
            TestSuites = results.TestSuites
        });";
        return template;
    }

    [McpServerTool, Description("if user asks to write a a fuzzing test for specific endpoint, you can get a c sharp template code of how to implement remote fuzzing test for a specific endpoint")]
    public static async Task<string> GetCSharpTemplateCodeOfHowToImplementRemoteFuzzingFromDockerTest()
    {
        var template = """
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
        """;
        return template;
    }

}
