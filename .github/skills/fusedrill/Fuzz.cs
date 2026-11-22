#!/usr/bin/env dotnet

#:package FuseDrill.Core@*-*
#:package DiffPlex@*

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using FuseDrill.Core;

var targets = new[]
{
    new Target(
        Label: "baseline",
        BaseUrl: "http://localhost:3000/api",
        OpenApiUrl: "http://localhost:3000/public/openapi3.json",
        Authorization: "Basic YWRtaW46YWRtaW4=",
        Seed: 12345,
        Excluded: new[]
        {
            "ChangeUserPassword_http_put_Async",
            "UpdateSignedInUser_http_put_Async"
        }),
    new Target(
        Label: "next-version",
        BaseUrl: "http://localhost:3001/api",
        OpenApiUrl: "http://localhost:3001/public/openapi3.json",
        Authorization: "Basic YWRtaW46YWRtaW4=",
        Seed: 12345,
        Excluded: new[]
        {
            "ChangeUserPassword_http_put_Async",
            "UpdateSignedInUser_http_put_Async"
        })
};

var serializerOptions = SerializerOptions.GetOptions();
serializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver();

foreach (var target in targets)
{
    using var httpClient = new HttpClient { BaseAddress = new Uri(target.BaseUrl) };
    if (AuthenticationHeaderValue.TryParse(target.Authorization, out var authorization))
    {
        httpClient.DefaultRequestHeaders.Authorization = authorization;
    }
    httpClient.Timeout = TimeSpan.FromMinutes(10);

    var excluded = target.Excluded.ToHashSet(StringComparer.Ordinal);
    var fuzzer = new ApiFuzzer(httpClient, target.OpenApiUrl, seed: target.Seed);
    var results = await fuzzer.TestWholeApi(apiCall => !excluded.Contains(apiCall.MethodName));

    var output = $"snapshot-{target.Label}.json";
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(results, serializerOptions));

    var totalCalls = results.TestSuites.Sum(s => s.ApiCalls.Count);
    Console.WriteLine($"TARGET={target.Label} SEED={results.Seed} SUITES={results.TestSuites.Count} CALLS={totalCalls} OUTPUT={output}");
}

var baselineText = await File.ReadAllTextAsync($"snapshot-{targets[0].Label}.json");
var nextText = await File.ReadAllTextAsync($"snapshot-{targets[1].Label}.json");
var diffModel = new InlineDiffBuilder(new Differ()).BuildDiffModel(oldText: baselineText, newText: nextText);
var diffLines = diffModel.Lines.Select(line => line.Type switch
{
    ChangeType.Deleted => $"- {line.Text}",
    ChangeType.Inserted => $"+ {line.Text}",
    ChangeType.Modified => $"~ {line.Text}",
    ChangeType.Unchanged => $"  {line.Text}",
    _ => string.Empty
});
var diffOutput = "fusedrill_diff.txt";
await File.WriteAllTextAsync(diffOutput, string.Join(Environment.NewLine, diffLines));
Console.WriteLine($"DIFF={diffOutput}");

record Target(string Label, string BaseUrl, string OpenApiUrl, string? Authorization, int Seed, string[] Excluded);
