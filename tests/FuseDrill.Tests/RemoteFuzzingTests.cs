using FuseDrill;
using FuseDrill.Core;
using NSwag;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace tests;

// Define a test collection
[CollectionDefinition("Sequential Tests", DisableParallelization = true)]

public class RemoteFuzzingTests
{
#if DEBUG
    [Fact(Skip = "Calls remote apis")]// calling external api guru endpoint, good for debugging.
#endif
    //https://api.apis.guru/v2/openapi.yaml
    public async Task ApiGuruYamlTest()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.apis.guru/v2")
        };

        var tester = new ApiFuzzerWithVerifier(httpClient, "https://api.apis.guru/v2/openapi.yaml");
        await tester.TestWholeApi();
    }

    #if DEBUG
        [Fact(Skip = "Calls remote apis")]// calling external api guru endpoint, good for debugging.
    #endif
    public async Task ExoscaleTest()
    {
        var key = Environment.GetEnvironmentVariable("exoscale", EnvironmentVariableTarget.User);
        var innerHandler = new ExoscaleAuthHandler("EXOccf871a2aae570ba3998f09c", key)
        {
            InnerHandler = new HttpClientHandler()
        };

        var httpClient = new HttpClient(innerHandler)
        {
            BaseAddress = new Uri("https://api-ch-gva-2.exoscale.com/v2")
        };

        var tester = new ApiFuzzerWithVerifier(httpClient, "https://openapi-v2.exoscale.com/source.json");
        await tester.TestWholeApi(apiCall => apiCall.HttpMethod=="get");
    }

    #if DEBUG
        [Fact(Skip = "Calls remote apis")]// calling external api guru endpoint, good for debugging.
    #endif
    public async Task TestAllAlotOfApis()
    {
        var seed = 1234567;
        var json = File.ReadAllText("list.json");
        var data = ParseAllServices(json);
        var openApi3VersionData = data.Where(item => item.OpenApiVersion == "3.0.0").ToList();

        var HttpClientForOpenApiDownalod = new HttpClient();
        var allCount = openApi3VersionData.Count;
        var index = 0;
        foreach (var item in openApi3VersionData)
        {
            item.Progress = $@"{index++}/{allCount}";
            var response = await HttpClientForOpenApiDownalod.GetAsync(item.OpenApiUrl);
            var swaggerContent = await response.Content.ReadAsStringAsync();

            OpenApiDocument document = null;
            try
            {
                document = await OpenApiDocument.FromJsonAsync(swaggerContent);
            }
            catch (Exception ex)
            {
                //put into one single line
                item.OpenApiJsonParserFailError = InlineMessage(ex?.ToString());
            }

            try
            {
                var firstServer = document?.Servers?.FirstOrDefault();
                var baseUrl = firstServer?.Url?.ToString() ?? throw new Exception("No valid base url provided in open api spec");

                if (baseUrl.Contains("{")) // it means it has variables, we need to replace them
                {
                    var variable = firstServer?.Variables?.FirstOrDefault() ?? throw new Exception("No valid server variable provided in open api spec");
                    var key = variable.Key;
                    var value = variable.Value;
                    var defaultVariable = firstServer?.Variables.FirstOrDefault().Value.Default;
                    baseUrl = baseUrl.Replace("{" + key + "}", defaultVariable);
                }

                item.BaseUrl = baseUrl;

                var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri(baseUrl)
                };

                var tester = new ApiFuzzer(httpClient, item.OpenApiUrl, seed, callEndpoints: false); // code smell two times to download open api spec;
                var results = await tester.TestWholeApi();

                item.Results = results;
                item.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                item.RunErrors = InlineMessage(ex?.ToString());
            }
        }
        var settings = new VerifySettings();
        settings.UseStrictJson();
        settings.DontScrubGuids();
        settings.DontIgnoreEmptyCollections();
        settings.IncludePrimitiveMembers();

        await Verifier.Verify(openApi3VersionData, settings);
    }

    static string InlineMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        // Remove newlines
        string cleanedMessage = message.Replace("\n", " ").Replace("\r", " ").Replace("\r\n", " ");

        // Trim any trailing spaces or commas
        return cleanedMessage.Trim(new char[] { ' ', ',' });
    }

    public static List<ApiResult> ParseAllServices(string jsonData)
    {

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        Dictionary<string, ApiCollection> apiData =
                   JsonSerializer.Deserialize<Dictionary<string, ApiCollection>>(jsonData, options);

        var apiList = new List<ApiResult>();

        foreach (var item in apiData)
        {
            var api = new ApiResult();
            api.Name = item.Key;
            //get latest version
            var version = item.Value.Versions.Keys.OrderByDescending(x => x).FirstOrDefault();
            api.OpenApiVersion = item.Value.Versions[version].OpenApiVersion;
            api.OpenApiUrl = item.Value.Versions[version].SwaggerUrl;
            apiList.Add(api);
        }

        return apiList;
    }
}


public class ApiResult
{
    public string Name { get; set; }

    public string OpenApiUrl { get; set; }

    public bool IsSuccessful { get; set; } = false;

    public string BaseUrl { get; set; }
    public string Progress { get; set; }

    public string OpenApiVersion { get; set; }

    public string OpenApiJsonParserFailError { get; set; }

    public string CompilationFailErrors { get; set; }

    public string RunErrors { get; set; }

    public FuzzerTests Results { get; set; }

    public string OpenApiJsonContent { get; set; } = null;

}

public class ApiCollection
{
    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("preferred")]
    public string Preferred { get; set; }

    [JsonPropertyName("versions")]
    public Dictionary<string, VersionInfo> Versions { get; set; }
}

public class VersionInfo
{
    [JsonPropertyName("added")]
    public DateTime Added { get; set; }

    [JsonPropertyName("info")]
    public ApiInfo Info { get; set; }

    [JsonPropertyName("updated")]
    public DateTime Updated { get; set; }

    [JsonPropertyName("swaggerUrl")]
    public string SwaggerUrl { get; set; }

    [JsonPropertyName("swaggerYamlUrl")]
    public string SwaggerYamlUrl { get; set; }

    [JsonPropertyName("openapiVer")]
    public string OpenApiVersion { get; set; }

    [JsonPropertyName("link")]
    public string Link { get; set; }
}

public class ApiInfo
{
    [JsonPropertyName("contact")]
    public ContactInfo Contact { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("x-apisguru-categories")]
    public List<string> Categories { get; set; }

    [JsonPropertyName("x-logo")]
    public LogoInfo Logo { get; set; }

    [JsonPropertyName("x-origin")]
    public List<OriginInfo> Origins { get; set; }

    [JsonPropertyName("x-providerName")]
    public string ProviderName { get; set; }

    [JsonPropertyName("x-serviceName")]
    public string ServiceName { get; set; }
}

public class ContactInfo
{
    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class LogoInfo
{
    [JsonPropertyName("backgroundColor")]
    public string BackgroundColor { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class OriginInfo
{
    [JsonPropertyName("format")]
    public string Format { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }
}

//{
//  ""1forge.com"": {
//    ""added"": ""2017-05-30T08:34:14.000Z"",
//    ""preferred"": ""0.0.1"",
//    ""versions"": {
//      ""0.0.1"": {
//        ""added"": ""2017-05-30T08:34:14.000Z"",
//        ""swaggerUrl"": ""https://api.apis.guru/v2/specs/1forge.com/0.0.1/swagger.json"",
//        ""openapiVer"": ""2.0""
//      }
//    }
//  },
//  ""1password.com:events"": {
//    ""added"": ""2021-07-19T10:17:09.188Z"",
//    ""preferred"": ""1.0.0"",
//    ""versions"": {
//      ""1.0.0"": {
//        ""added"": ""2021-07-19T10:17:09.188Z"",
//        ""swaggerUrl"": ""https://api.apis.guru/v2/specs/1password.com/events/1.0.0/openapi.json"",
//        ""openapiVer"": ""3.0.0""
//      }
//    }
//  }
//}";