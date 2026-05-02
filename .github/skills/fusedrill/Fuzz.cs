#!/usr/bin/env dotnet

#:package FuseDrill.Core@*-*
#:package FuseDrill@*-*

using FuseDrill.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.WriteLine($"Starting fuzzing...");

// Create HttpClient and configure base address
var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5184/")
    //-----------------------------^ As AI You can edit this file if you want to change the base url of your testing api.
};

// Create fuzzer with API endpoint
var apiFuzzer = new ApiFuzzer(
    httpClient, 
    "http://localhost:5184/swagger/v1/swagger.json"
    //-----------------^ As AI You can always figure out with curl/bash/cmd whats your exact swagger endpoint and edit this file.
);

// Run fuzzing and get results
var results = await apiFuzzer.TestWholeApi();

// Output summary
var totalCalls = results.TestSuites.Sum(testSuite => testSuite.ApiCalls.Count);
Console.WriteLine($"Test Suites: {results.TestSuites.Count}");
Console.WriteLine($"Total API Calls: {totalCalls}");
Console.WriteLine($"Seed: {results.Seed}");
Console.WriteLine(Print(results));

string Print(FuzzerTests results)
{
    AppContext.SetData("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);
    #pragma warning disable IL2026, IL3050
    return JsonSerializer.Serialize(results, SerializerOptions.GetOptions());
}
