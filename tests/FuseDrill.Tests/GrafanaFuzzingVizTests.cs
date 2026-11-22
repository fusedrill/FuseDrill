using DotNet.Testcontainers.Builders;
using FuseDrill.Core;
using System.Net.Http.Headers;
using System.Text;
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
        byte[] bytes = Encoding.UTF8.GetBytes(text);

        int width = (int)Math.Ceiling(Math.Sqrt(bytes.Length));
        int height = (int)Math.Ceiling((double)bytes.Length / width);
        int rowStride = ((width * 3 + 3) / 4) * 4;
        int imageSize = rowStride * height;

        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(54 + imageSize);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        var row = new byte[rowStride];
        for (int y = height - 1; y >= 0; y--)
        {
            Array.Clear(row);
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                byte value = index < bytes.Length ? bytes[index] : (byte)0;
                row[x * 3] = value;
                row[x * 3 + 1] = value;
                row[x * 3 + 2] = value;
            }

            writer.Write(row);
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
