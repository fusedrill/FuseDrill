using FuseDrill;
using FuseDrill.Core;
using Xunit;

namespace tests;

[Collection("Sequential Tests")]
public class CoverageGuidedFuzzingTests
{
    [Fact]
    public async Task TestCoverageGuidedFuzzingWithMinimumConfiguration()
    {
        var options = new FuzzingOptions
        {
            EnableCoverageGuidance = true,
            EnableEdgeCaseGeneration = true,
            EnableCombinatorialTesting = true,
            EnableMutation = true,
            MaxIterations = 100,
            MaxDurationSeconds = 60,
            TargetCoverage = 80.0,
            MutationCount = 5,
            Logger = msg => Console.WriteLine($"[FUZZER] {msg}")
        };

        var fuzzer = new ApiFuzzerWithVerifier<Program>();
        var result = await fuzzer.TestWholeApiWithCoverageGuidance(options);

        Assert.NotNull(result);
        Assert.NotEmpty(result.TestSuites);
        
        var totalApiCalls = result.TestSuites.Sum(s => s.ApiCalls.Count);
        Assert.True(totalApiCalls > 0, $"Expected some API calls, got {totalApiCalls}");

        var report = fuzzer.GetCoverageReport();
        Assert.NotNull(report);
        Assert.NotNull(report.FuzzingMetrics);
        Assert.True(report.FuzzingMetrics.TotalApiCalls > 0, "Should have tracked API calls");
        Assert.True(report.FuzzingMetrics.TotalDuration.TotalSeconds >= 0);

        Console.WriteLine("\n========== COVERAGE REPORT ==========");
        Console.WriteLine($"Total API Calls: {report.FuzzingMetrics.TotalApiCalls}");
        Console.WriteLine($"Unique Inputs Generated: {report.FuzzingMetrics.UniqueInputsGenerated}");
        Console.WriteLine($"Edge Cases Generated: {report.FuzzingMetrics.EdgeCasesGenerated}");
        Console.WriteLine($"Combinations Generated: {report.FuzzingMetrics.CombinationsGenerated}");
        Console.WriteLine($"Exceptions Encountered: {report.FuzzingMetrics.ExceptionsEncountered}");
        Console.WriteLine($"Total Duration: {report.FuzzingMetrics.TotalDuration.TotalSeconds:F2}s");
        Console.WriteLine($"Unique Exceptions: {report.FuzzingMetrics.UniqueExceptions}");
        if (report.FuzzingMetrics.DiscoveredExceptionTypes.Count > 0)
        {
            Console.WriteLine($"Exception Types: {string.Join(", ", report.FuzzingMetrics.DiscoveredExceptionTypes)}");
        }
        Console.WriteLine($"Unique Coverage Points: {report.UniqueCoveragePoints}");
        Console.WriteLine($"Minimization: {report.FuzzingMetrics.InputsBeforeMinimization} -> {report.FuzzingMetrics.InputsAfterMinimization} ({report.FuzzingMetrics.MinimizationReductionPercent}% reduction)");
        Console.WriteLine($"Unique Behaviors: {report.FuzzingMetrics.UniqueBehaviors}");
        Console.WriteLine("=====================================\n");
    }

    [Fact]
    public async Task TestCoverageGuidedFuzzingWithEdgeCasesOnly()
    {
        var options = new FuzzingOptions
        {
            EnableCoverageGuidance = false,
            EnableEdgeCaseGeneration = true,
            EnableCombinatorialTesting = false,
            EnableMutation = false,
            MaxIterations = 50,
            MaxDurationSeconds = 30,
            Logger = msg => Console.WriteLine($"[EDGE CASES] {msg}")
        };

        var fuzzer = new ApiFuzzerWithVerifier<Program>();
        var result = await fuzzer.TestWholeApiWithCoverageGuidance(options);

        Assert.NotNull(result);
        Assert.NotEmpty(result.TestSuites);
        
        var report = fuzzer.GetCoverageReport();
        Assert.NotNull(report);
    }

    [Fact]
    public async Task TestCoverageGuidedFuzzingWithCombinatorialOnly()
    {
        var options = new FuzzingOptions
        {
            EnableCoverageGuidance = false,
            EnableEdgeCaseGeneration = false,
            EnableCombinatorialTesting = true,
            EnableMutation = false,
            MaxIterations = 50,
            MaxDurationSeconds = 30,
            Logger = msg => Console.WriteLine($"[COMBINATORIAL] {msg}")
        };

        var fuzzer = new ApiFuzzerWithVerifier<Program>();
        var result = await fuzzer.TestWholeApiWithCoverageGuidance(options);

        Assert.NotNull(result);
        Assert.NotEmpty(result.TestSuites);
        
        var report = fuzzer.GetCoverageReport();
        Assert.NotNull(report);
    }

    [Fact]
    public async Task TestCoverageReportGeneration()
    {
        var fuzzer = new ApiFuzzerWithVerifier<Program>();

        fuzzer.ResetCoverage();
        var initialReport = fuzzer.GetCoverageReport();
        Assert.NotNull(initialReport);

        var options = new FuzzingOptions
        {
            EnableCoverageGuidance = true,
            EnableEdgeCaseGeneration = true,
            EnableCombinatorialTesting = true,
            EnableMutation = true,
            MaxIterations = 25,
            MaxDurationSeconds = 15,
            Logger = msg => { }
        };

        await fuzzer.TestWholeApiWithCoverageGuidance(options);

        var finalReport = fuzzer.GetCoverageReport();
        Assert.NotNull(finalReport);
        Assert.True(finalReport.UniqueCoveragePoints >= 0);
    }

    [Fact]
    public async Task TestCoverageGuidedWithFilter()
    {
        var options = new FuzzingOptions
        {
            EnableCoverageGuidance = true,
            EnableEdgeCaseGeneration = true,
            EnableCombinatorialTesting = true,
            EnableMutation = true,
            MaxIterations = 50,
            MaxDurationSeconds = 30,
            Filter = apiCall => apiCall.MethodName.Contains("Todo", StringComparison.OrdinalIgnoreCase) ||
                                 apiCall.MethodName.Contains("Weather", StringComparison.OrdinalIgnoreCase),
            Logger = msg => Console.WriteLine($"[FILTERED] {msg}")
        };

        var fuzzer = new ApiFuzzerWithVerifier<Program>();
        var result = await fuzzer.TestWholeApiWithCoverageGuidance(options);

        Assert.NotNull(result);
        
        var report = fuzzer.GetCoverageReport();
        Assert.NotNull(report);
    }

    [Fact]
    public async Task TestMutationOnlyFuzzing()
    {
        var options = new FuzzingOptions
        {
            EnableCoverageGuidance = false,
            EnableEdgeCaseGeneration = false,
            EnableCombinatorialTesting = false,
            EnableMutation = true,
            MaxIterations = 100,
            MaxDurationSeconds = 60,
            MutationCount = 10,
            Logger = msg => Console.WriteLine($"[MUTATION] {msg}")
        };

        var fuzzer = new ApiFuzzerWithVerifier<Program>();
        var result = await fuzzer.TestWholeApiWithCoverageGuidance(options);

        Assert.NotNull(result);
        Assert.NotEmpty(result.TestSuites);
        
        var report = fuzzer.GetCoverageReport();
        Assert.NotNull(report);
    }
}
