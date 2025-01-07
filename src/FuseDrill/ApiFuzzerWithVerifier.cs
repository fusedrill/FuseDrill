using FuseDrill.Core;

namespace FuseDrill;
public class ApiFuzzerWithVerifier : IApiFuzzer
{
    private readonly ApiFuzzer _apiFuzzer;

    /// <summary>
    /// Remote API fuzzing
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="openApiUrl"></param>
    public ApiFuzzerWithVerifier(HttpClient httpClient, string openApiUrl = "/swagger/v1/swagger.json", int seed = 1234567, bool callEndpoints = true)
    {
        _apiFuzzer = new ApiFuzzer(httpClient, openApiUrl, seed);
    }

    /// <summary>
    /// Consumer provides any class that implements client with open api methods.
    /// </summary>
    /// <param name="openApiClientClassInstance"></param>
    public ApiFuzzerWithVerifier(object openApiClientClassInstance, int seed = 1234567, bool callEndpoints = true)
    {
        _apiFuzzer = new ApiFuzzer(openApiClientClassInstance, seed, callEndpoints);
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
        return testSuitesProcessed;// not reachable just preserving same interface;
    }
}