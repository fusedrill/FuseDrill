using FuseDrill.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FuseDrill;

public class ApiFuzzer<TEntryPoint> : IApiFuzzer where TEntryPoint : class
{
    WebApplicationFactory<TEntryPoint> _factory;

    private readonly ApiFuzzer _apiFuzzer;

    /// <summary>
    /// Fuzzing with sensible defaults, using web application factory.
    /// </summary>
    public ApiFuzzer(int seed = 1234567, string openApiPath = "/swagger/v1/swagger.json", bool callEndpoints = true) //Todo can we detec this url dynamically from webapplication? 
    {
        var testApplication = new TestApplication<TEntryPoint>();
        var inMemoryClient = testApplication.CreateClient();
        _apiFuzzer = new ApiFuzzer(inMemoryClient, inMemoryClient, openApiPath, seed, callEndpoints);
    }

    public async Task<FuzzerTests> TestWholeApi(Func<ApiCall, bool> filter = null) => await _apiFuzzer.TestWholeApi(filter);

}