using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using FuseDrill.Core;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Writers;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SharpYaml.Serialization;

namespace FuseDrill.MCP.Tools;

[McpServerToolType, Description("This MCP tool is already running.")]
public class FuseDrillTools
{
    [McpServerTool, Description("Check if the MCP tool is running")]
    public static async Task<string> Fizz()
    {
        return $"Fuzz! The MCP tool is running";
    }

    [McpServerTool, Description("This MCP tool is already running. FirstPassFuzzing: Run first pass fuzzing in order to know about the API shape, You need to figure out first inputs of the API using curl or other tool, then you can use this tool to run first pass fuzzing on the API")]
    public static async Task<string> RunFirstPassFuzzing(
        [Description("(First figure out if server hostname and port if it is running using curl) Base URL for the API")] string baseUrl,
        [Description("(First figure out if server hostname and port if it is running using curl) OpenAPI URL for the API")] string openAPIUrl,
        [Description("(Optional) Ask user if the testing service requires authentication, this is test server so security is not a concern")] string? AuthorizationHeader)
    {
        try
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(baseUrl);

            //auth header is optional
            if (!string.IsNullOrEmpty(AuthorizationHeader))
            {
                if (AuthenticationHeaderValue.TryParse(AuthorizationHeader, out AuthenticationHeaderValue authHeader))
                    httpClient.DefaultRequestHeaders.Authorization = authHeader;
                else{
                    throw new McpProtocolException("Invalid Authorization header format. Expected 'Bearer <token>' or 'Basic <credentials>'.", McpErrorCode.InvalidRequest);
                }
            }

            var fuzzer = new ApiFuzzer(httpClient, openAPIUrl);

            var results = await fuzzer.TestWholeApi();

        var successResponse = new
            {
                Success = true,
                BaseUrl = baseUrl,
                OpenAPIUrl = openAPIUrl,
                TestSuites = results,
                NextSteps = """
                Now you can use the results to understand the API shape and run specific endpoint fuzzing tests.
                But first you need to ask a user what they want to do next:
                    Fuzz test a specific endpoint. (Provide the endpoint names for user to chose from).
                    Write a brand new fuzzing test for all endpoints for this api.
                    Fuzz all except certain endpoints - Useful if some endpoints might break others (like DELETE)
                """
            };

            var successResponseJson = JsonSerializer.Serialize(successResponse, SerializerOptions.GetOptions());

            // Return serialized test details
            return successResponseJson;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                Success = false,
                Error = ex.Message,
                ErrorType = ex.GetType().Name,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException?.Message,
                InnerExceptionType = ex.InnerException?.GetType().Name,
                BaseUrl = baseUrl,
                OpenAPIUrl = openAPIUrl
            }, SerializerOptions.GetOptions());
        }
    }

    [McpServerTool, Description("Now that you know the api shape from FirstPassFuzzing, you can fuzz all endpoints with excluding list, this is for usecases where endpoints break other apis, like ChangePassord, UpdateUser etc. You can use this tool to run fuzzing on all endpoints except the ones you specify in the excludedEndpointsNames list")]
    public static async Task<string> RunAllEndpointsWithExcludingList(
        [Description("Excluded endpoints names")] List<string> excludedEndpointsNames,
        [Description("Base URL for the API")] string baseUrl,
        [Description("OpenAPI URL for the API")] string OpenAPIUrl,
        [Description("(Optional) Ask user if the service requires authentication, example: 'Bearer <token>' or 'Basic <credentials>'")] string? AuthorizationHeader)

    {
        var httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(baseUrl))
        {
            httpClient.BaseAddress = new Uri(baseUrl);
        }

        //auth header is optional
        if (!string.IsNullOrEmpty(AuthorizationHeader))
        {
            if (AuthenticationHeaderValue.TryParse(AuthorizationHeader, out AuthenticationHeaderValue authHeader))
                httpClient.DefaultRequestHeaders.Authorization = authHeader;
            else
                throw new McpProtocolException("Invalid Authorization header format. Expected 'Bearer <token>' or 'Basic <credentials>'.", McpErrorCode.InvalidRequest);
        }

        var fuzzer = new ApiFuzzer(httpClient, OpenAPIUrl);
        var results = await fuzzer.TestWholeApi(apiCall =>
        {
            var filterEndpoint = excludedEndpointsNames.All(name => !apiCall.MethodName.EndsWith(name));

            return filterEndpoint;
        });

        // Return serialized test details
        return JsonSerializer.Serialize(new
        {
            excludedEndpointsNames = excludedEndpointsNames,
            BaseUrl = baseUrl,
            OpenAPIUrl = OpenAPIUrl,
            TestSuites = results.TestSuites,
            NextSteps = "Ask user if he want to make a reproducible fuzzing test for these settings."
        },SerializerOptions.GetOptions());
    }


    [McpServerTool, Description("Now that you know the api shape from FirstPassFuzzing, you can run a specific ai enhanced test for single endpoint")]
    public static async Task<string> RunSpecificEndpointFuzzing(
        [Description("The API endpoint to test")] string endpoint,
        [Description("Base URL for the API")] string baseUrl,
        [Description("OpenAPI URL for the API")] string OpenAPIUrl,
        [Description("The API request parameters (This information you can get from FirstPassFuzzing)")] List<ParameterValue> RequestParameters,
        [Description("(Optional) Ask user if the service requires authentication, example: 'Bearer <token>' or 'Basic <credentials>'")] string? AuthorizationHeader)
    {
        var httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(baseUrl))
        {
            httpClient.BaseAddress = new Uri(baseUrl);
        }
        
        //auth header is optional
        if (!string.IsNullOrEmpty(AuthorizationHeader))
        {
            if (AuthenticationHeaderValue.TryParse(AuthorizationHeader, out AuthenticationHeaderValue authHeader))
                httpClient.DefaultRequestHeaders.Authorization = authHeader;
            else
                throw new McpProtocolException("Invalid Authorization header format. Expected 'Bearer <token>' or 'Basic <credentials>'.", McpErrorCode.InvalidRequest);
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
        }, SerializerOptions.GetOptions());
    }

    [McpServerTool, Description("if user asks to write a a fuzzing test for specific endpoint, you can get a c sharp template code of how to implement remote fuzzing test for a specific endpoint")]
    public static async Task<string> GetCSharpTemplateCodeFuzzSpecificEndpoint()
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
        },SerializerOptions.GetOptions());";
        return template;
    }

    [McpServerTool, Description("if user asks to write a a fuzzing test for all endpoints, you can get a c sharp template code of how to implement remote fuzzing test for all endpoints. Dont change the template to much only the nececary values")]
    public static async Task<string> GetCSharpTemplateCodeFuzzAllEndpoints()
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
