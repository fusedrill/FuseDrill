using DotNet.Testcontainers.Builders;
using FuseDrill.Core;
using System.Net.Http.Headers;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;

namespace tests;

// Define a test collection
[CollectionDefinition("Sequential Tests", DisableParallelization = true)]
public class GrafanaFuzzingVizTests
{
    [Fact(Skip = "Only good for testing/POCS")]
    public async Task GrafanaDockerTest()
    {
        //http://localhost:3000/public/openapi3.json
        //docker remove grafana
        //docker run -d --name=grafana -p 3000:3000 grafana/grafana
        //pick older version

        var dockerImageUrl = "grafana/grafana:11.3.8"; // Use the latest Grafana image
        var containerName = "grafana";

        var dockerImageUrlLatest = "grafana/grafana:latest"; // Use the latest Grafana image

        var grafana11V = await DiffByVersion(dockerImageUrl, containerName);
        var grafanaLatestV = await DiffByVersion(dockerImageUrlLatest, containerName);

        // Compare the two versions using my diff function

        var diff = SimpleDiffer.GenerateDiff(grafana11V, grafanaLatestV);

        // write to file 
        var filePath = "grafana_diff.txt";
        await System.IO.File.WriteAllTextAsync(filePath, diff);

        // create binary image of a grafana11V text using ImageSharp
        CreateBinaryVisualization(grafana11V, "grafana11V.png");
        CreateBinaryVisualization(grafanaLatestV, "grafanaLatestV.png");

        // create image of the diff
        CreateBinaryVisualization(diff, "grafana_diff.png");
    
        // open all images using default image viewer
        OpenImage("grafana11V.png");
        OpenImage("grafanaLatestV.png");
        OpenImage("grafana_diff.png");



        static async Task<string> DiffByVersion(string dockerImageUrl, string containerName)
        {

            // Grafana settings
            // http://localhost:3000/api
            // http://localhost:3000/public/openapi3.json
            // Basic YWRtaW46YWRtaW4=

            // Set up a TestContainer for the image
            var containerBuilder = new ContainerBuilder()
                .WithImage(dockerImageUrl)
                .WithName(containerName)
                .WithPortBinding(3000, 3000);

            var container = containerBuilder.Build();
            await container.StartAsync();
            await Task.Delay(10000);

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

            var option = SerializerOptions.GetOptions();
            // return comparison as string
            return System.Text.Json.JsonSerializer.Serialize(testSuitesProcessed, option);
        }
    }

    private static void CreateBinaryVisualization(string text, string outputPath)
    {
        // Convert text to bytes
        byte[] bytes = Encoding.UTF8.GetBytes(text);

        // Calculate dimensions for a roughly square image
        int width = (int)Math.Ceiling(Math.Sqrt(bytes.Length));
        int height = (int)Math.Ceiling((double)bytes.Length / width);

        // Create a new image
        using (var image = new Image<Rgba32>(width, height))
        {
            // Fill the image with black background
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        int index = y * width + x;
                        if (index < bytes.Length)
                        {
                            // Create a color based on the byte value
                            byte value = bytes[index];
                            row[x] = new Rgba32(value, value, value, 255);
                        }
                        else
                        {
                            // Fill remaining pixels with black
                            row[x] = new Rgba32(0, 0, 0, 255);
                        }
                    }
                }
            });

            // Save the image
            image.Save(outputPath);
        }
    }

    private static void OpenImage(string path)
    {
        if (File.Exists(path))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
    }
}
