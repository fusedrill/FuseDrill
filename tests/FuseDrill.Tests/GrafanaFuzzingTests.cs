using FuseDrill;
using FuseDrill.Core;
using NSwag;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace tests;

// Define a test collection
[CollectionDefinition("Sequential Tests", DisableParallelization = true)]

public class GrafanaFuzzingTests
{
    #if DEBUG
        [Fact(Skip = "Need to run docker")]
    #endif
    //http://localhost:3000/public/openapi3.json
    //docker run -d --name=grafana -p 3000:3000 grafana/grafana
    public async Task GrafanaJsonTest()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:3000/api"),
        };

        //add basic auth header here 
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:admin")));

        var tester = new ApiFuzzerWithVerifier(httpClient, "http://localhost:3000/public/openapi3.json");
        await tester.TestWholeApi();
    }
}