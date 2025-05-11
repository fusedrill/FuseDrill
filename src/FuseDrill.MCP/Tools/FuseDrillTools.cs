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

    [McpServerTool, Description("FirstPassFuzzing: Run first pass fuzzing in order to know about the API shape")]
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
            TestSuites = results.TestSuites
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
        });
    }

    // [McpServerTool, Description("Analyze OpenAPI specification for issues and coverage")]
    // public static async Task<string> AnalyzeOpenApiSpec(
    //     [Description("URL to the OpenAPI specification")] string specUrl)
    // {
    //     // Read and parse OpenAPI document using the service
    //     var document = await _apiAnalysisService.LoadSpecification(specUrl);

    //     using var stringWriter = new StringWriter();
    //     var jsonWriter = new OpenApiJsonWriter(stringWriter);
    //     document.SerializeAsV3(jsonWriter);
    //     var json = stringWriter.ToString();
    //     // just return as json
    //     return json;
    // }

    // [McpServerTool, Description("Compare API fuzzing results between versions")]
    // public static async Task<string> CompareApiFuzzVersions(
    //     [Description("Previous version API spec URL")] string oldVersionUrl,
    //     [Description("Current version API spec URL")] string newVersionUrl)
    // {
    //     // Use the API analysis service to compare specs
    //     var changes = await _apiAnalysisService.CompareSpecs(oldVersionUrl, newVersionUrl);

    //     var comparison = new
    //     {
    //         TotalChanges = changes.Count(),
    //         BreakingChanges = changes.Count(c => IsBreakingChange(c)),
    //         AddedEndpoints = changes.Count(c => c.StartsWith("+")),
    //         RemovedEndpoints = changes.Count(c => c.StartsWith("-")),
    //         ModifiedEndpoints = changes.Count(c => c.StartsWith("~")),
    //         Details = changes
    //     };

    //     return JsonSerializer.Serialize(comparison);
    // }

    // private static bool IsBreakingChange(string change)
    // {
    //     return change.StartsWith("-") || // Removed elements
    //            change.Contains("required") || // Changed requirements
    //            Regex.IsMatch(change, @"type.*=>.*type"); // Type changes
    // }
}
