using DotNet.Testcontainers.Builders;
using FuseDrill.Core;
using System.Net.Http.Headers;
using System.Text;

namespace tests;

// Define a test collection
[CollectionDefinition("Sequential Tests", DisableParallelization = true)]
public class GrafanaFuzzingTests
{
    [Fact]
    public async Task GrafanaDockerTest()
    {
        //http://localhost:3000/public/openapi3.json
        //docker remove grafana
        //docker run -d --name=grafana -p 3000:3000 grafana/grafana

        var dockerImageUrl = "grafana/grafana:latest"; // Change this to your actual image URL
        var containerName = "grafana";

        // Set up a TestContainer for the image
        var containerBuilder = new ContainerBuilder()
            .WithImage(dockerImageUrl)
            .WithName(containerName)
            .WithPortBinding(3000, 3000);

        var container = containerBuilder.Build();
        await container.StartAsync();
        await Task.Delay(7000);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:3000/api"),
        };

        //add basic auth header here 
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:admin")));

        var tester = new ApiFuzzer(httpClient, "http://localhost:3000/public/openapi3.json");
        var testSuitesProcessed = await tester.TestWholeApi(apicall =>
                apicall.MethodName != "ChangeUserPassword_http_put_Async" &&
                apicall.MethodName != "UpdateSignedInUser_http_put_Async"
                );

        var settings = new VerifySettings();
        settings.UseStrictJson();
        settings.ScrubInlineGuids();
        settings.DontIgnoreEmptyCollections();
        settings.ScrubMember<object>("Uid");
        settings.ScrubMember<object>("CreatedAt");
        settings.ScrubMember<object>("Code");
        settings.ScrubMember<object>("Url");
        settings.ScrubMember<object>("EndDate");
        settings.ScrubMember<object>("StartDate");
        settings.ScrubMember<object>("LastSeenAt");
        settings.ScrubMember<object>("Created");
        settings.ScrubMember<object>("Updated");
        settings.ScrubMember<object>("UpdatedAt");

        await container.StopAsync();
        await container.DisposeAsync();

        await Verify(testSuitesProcessed, settings);
    }
}
