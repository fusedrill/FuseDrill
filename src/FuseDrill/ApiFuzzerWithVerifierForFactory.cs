using FuseDrill.Core;
using FuseDrill.Core.Coverage;

namespace FuseDrill;

public class ApiFuzzerWithVerifier<TEntryPoint> : IApiFuzzer where TEntryPoint : class
{
    private readonly ApiFuzzer<TEntryPoint> _apiFuzzer;

    /// <summary>
    /// Fuzzing with sensible defaults, using web application factory.
    /// </summary>
    public ApiFuzzerWithVerifier(int seed = 1234567)
    {
        _apiFuzzer = new ApiFuzzer<TEntryPoint>(seed);
    }

    public async Task<FuzzerTests> TestWholeApi(Func<ApiCall, bool>? filter = null)
    {
        var settings = new VerifySettings();
        settings.UseStrictJson();
        settings.DontScrubGuids();
        settings.DontIgnoreEmptyCollections();
        settings.IncludePrimitiveMembers();

        var testSuitesProcessed = await _apiFuzzer.TestWholeApi(filter);

        await Verify(testSuitesProcessed, settings);
        return testSuitesProcessed;
    }

    public async Task<FuzzerTests> TestWholeApiWithCoverageGuidance(FuzzingOptions? options = null)
    {
        return await _apiFuzzer.TestWholeApiWithCoverageGuidance(options ?? new FuzzingOptions());
    }

    public CoverageReport? GetCoverageReport()
    {
        return _apiFuzzer.GetCoverageReport();
    }

    public void ResetCoverage()
    {
        _apiFuzzer.ResetCoverage();
    }
}