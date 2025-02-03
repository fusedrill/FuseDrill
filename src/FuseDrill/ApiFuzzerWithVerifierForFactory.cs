using FuseDrill.Core;

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

    public async Task<FuzzerTests> TestWholeApi(Func<ApiCall, bool> filter = null)
    {
        var settings = new VerifySettings();
        settings.UseStrictJson();
        settings.DontScrubGuids();
        settings.DontIgnoreEmptyCollections();
        settings.IncludePrimitiveMembers();

        var testSuitesProcessed = await _apiFuzzer.TestWholeApi(filter);

        //render tree currently does not work
        //var renders = testSuitesProcessed.Select(item => item.ToString()).ToList();
        //await Verify(renders, settings);

        await Verify(testSuitesProcessed, settings);
        return testSuitesProcessed; // not reachable just preserving same interface;
    }

}