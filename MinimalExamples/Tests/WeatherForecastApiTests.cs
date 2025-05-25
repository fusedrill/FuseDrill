using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FuseDrill.Core;
using Xunit;

namespace MinimalExamples.Tests
{
    public class WeatherForecastApiTests
    {
        //[Fact] // Manulally run this test, as it requires a running API
        public async Task RemoteFuzzing_WeatherForecastEndpoint_Works()
        {
            var baseUrl = "http://localhost:5184";
            var openApiUrl = "http://localhost:5184/swagger/v1/swagger.json";
            var endpoint = "weatherforecast";
            var requestParameters = new List<ParameterValue>(); // No parameters for GET

            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(baseUrl);

            var fuzzer = new FuseDrill.Core.ApiFuzzer(httpClient, openApiUrl);
            var results = await fuzzer.TestWholeApi(apiCall =>
            {
                var filterEndpoint = apiCall.MethodName.EndsWith(endpoint);
                if (filterEndpoint)
                {
                    apiCall.RequestParameters = requestParameters;
                }
                return filterEndpoint;
            });

            var json = JsonSerializer.Serialize(new
            {
                Endpoint = endpoint,
                BaseUrl = baseUrl,
                OpenAPIUrl = openApiUrl,
                TestSuites = results.TestSuites
            });

            Assert.NotNull(results);
            Assert.NotNull(results.TestSuites);
        }
    }
}
