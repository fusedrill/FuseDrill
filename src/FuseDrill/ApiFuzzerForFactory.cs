using FuseDrill.Core;
using FuseDrill.Core.Coverage;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FuseDrill;

public class ApiFuzzer<TEntryPoint> : IApiFuzzer where TEntryPoint : class
{
    WebApplicationFactory<TEntryPoint> _factory;

    private readonly ApiFuzzer _apiFuzzer;

    /// <summary>
    /// Fuzzing with sensible defaults, using web application factory.
    /// </summary>
    public ApiFuzzer(int seed = 1234567, string openApiPath = "/swagger/v1/swagger.json")
    {
        var testApplication = new TestApplication<TEntryPoint>();
        var inMemoryClient = testApplication.CreateClient();
        _apiFuzzer = new ApiFuzzer(inMemoryClient, inMemoryClient, openApiPath, seed);
    }

    public async Task<FuzzerTests> TestWholeApi(Func<ApiCall, bool>? filter = null) => await _apiFuzzer.TestWholeApi(filter);

    public async Task<FuzzerTests> TestWholeApiWithCoverageGuidance(FuzzingOptions? options = null) => await _apiFuzzer.TestWholeApiWithCoverageGuidance(options ?? new FuzzingOptions());

    public CoverageReport? GetCoverageReport() => _apiFuzzer.GetCoverageReport();

    public void ResetCoverage() => _apiFuzzer.ResetCoverage();
}