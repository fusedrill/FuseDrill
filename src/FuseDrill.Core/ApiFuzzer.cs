using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using NJsonSchema;
using NJsonSchema.CodeGeneration;
using NSwag;
using NSwag.CodeGeneration.CSharp;
using NSwag.CodeGeneration.OperationNameGenerators;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using static FuseDrill.Core.DataGenerationHelper;
using FuseDrill.Core.Coverage;
using FuseDrill.Core.Generation;

namespace FuseDrill.Core;

public class ApiFuzzer : IApiFuzzer
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _httpClientForSwaggerDownload;
    private readonly object _openApiClientClassInstance;
    private readonly string _swaggerPath;
    private readonly string _baseurl;
    private readonly int _seed;
    private readonly bool _callEndpoints;
    
    private CoverageTracker? _coverageTracker;
    private CoverageReport? _lastCoverageReport;

    /// <summary>
    /// Remote API fuzzing
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="OpenAPIUrl"></param>
    public ApiFuzzer(HttpClient httpClient, string OpenAPIUrl, int seed = 1234567, bool callEndpoints = true)
    {
        _httpClient = httpClient;
        _httpClientForSwaggerDownload = new HttpClient();
        _swaggerPath = OpenAPIUrl ?? throw new Exception("OpenAPIUrl not provided in the httpclient.");
        _baseurl = httpClient?.BaseAddress?.ToString() ?? throw new Exception("Base url not provided in the httpclient.");
        _seed = seed;
        _callEndpoints = callEndpoints;
    }

    /// <summary>
    /// local API fuzzing
    /// </summary>
    public ApiFuzzer(HttpClient httpClient, HttpClient InMemoryHttpClientForSwaggerDownload, string OpenAPIUrl, int seed = 1234567, bool callEndpoints = true)
    {
        _httpClient = httpClient;
        _httpClientForSwaggerDownload = InMemoryHttpClientForSwaggerDownload; //Sometimes open api definitions different base address.
        _swaggerPath = OpenAPIUrl ?? throw new Exception("OpenAPIUrl not provided in the httpclient.");
        _baseurl = InMemoryHttpClientForSwaggerDownload?.BaseAddress?.ToString() ?? throw new Exception("Base url not provided in the httpclient.");
        _seed = seed;
        _callEndpoints = callEndpoints;
    }

    /// <summary>
    /// Consumer provides openApiClient
    /// </summary>
    /// <param name="openApiClientClassInstance"></param>
    public ApiFuzzer(object openApiClientClassInstance, int seed = 1234567, bool callEndpoints = true)
    {
        _openApiClientClassInstance = openApiClientClassInstance;
        _seed = seed;
        _callEndpoints = callEndpoints;
    }

    /// <summary>
    /// Fuzzes whole API, with all sorts of possible permutations
    /// </summary>
    /// <param name="filter">You can filter input testsuites before doing fuzzing </param>
    /// <returns></returns>
    public async Task<FuzzerTests> TestWholeApi(Func<ApiCall, bool> filter = null)
    {
        var testSuitesProcessed = new List<TestSuite>();
        var genericClientInstance = _openApiClientClassInstance ?? await GetOpenApiClientInstanceDynamically(_swaggerPath, _httpClientForSwaggerDownload);
        var apiClientAsData = new ApiShapeData(genericClientInstance);
        var dataGenerationHelper = new DataGenerationHelper(_seed);
        var testSuiteGen = dataGenerationHelper.CreateApiMethodPermutationsTestSuite(apiClientAsData);

        // Automatically get the path of the currently executing assembly
        //string assemblyPath = @"D:\main\Pocs\newpocs\tests\bin\Debug\net8.0\tests.dll";

        foreach (var testSuite in testSuiteGen)
        {
            // Step 1: Instrument the assembly to collect coverage for the current test suite
            //var coverage = InstrumentAssembly(assemblyPath);

            // apply filter on api calls data
            if (filter != null)
            {
                testSuite.ApiCalls = testSuite.ApiCalls.Where(filter).ToList();
            }
            else
            {
                testSuite.ApiCalls = testSuite.ApiCalls.ToList();
            }

            if (_callEndpoints)
            {
                //Step 2: Run your tests (simulate custom test execution)
                foreach (var apiCall in testSuite.ApiCalls)
                {
                    var api = await DoApiCall(genericClientInstance, apiCall);
                    apiCall.Response = ResolveResponse(api);
                    apiCall.Method = null; //Need to clean up the method.because its not serializable, or you can create DTO
                }
            }

            //RecreateFixture(_factory, _httpClient);
            //var result = coverage.GetCoverageResult();

            // Step 3: Calculate and set the test coverage percentage for the current test suite
            //testSuite.TestCoveragePercentage = CalculateCoveragePercentage(result);

            // Add the test suite results to the output list
            testSuitesProcessed.Add(testSuite);

        }

        //order api calls
        testSuitesProcessed.ForEach(suite => { suite.ApiCalls = suite.ApiCalls.OrderBy(item => item.ApiCallOrderId).ThenBy(item => item.MethodName).ToList(); });
        testSuitesProcessed.OrderBy(item => item.TestSuiteOrderId);


        var tests = new FuzzerTests();
        tests.TestSuites = testSuitesProcessed;
        tests.Seed = _seed;

        return tests;

    }

    private static dynamic ResolveResponse(ApiCall api)
    {
        var typeName1 = "ApiException`1";
        var typeName2 = "ApiException"; //TODO improve exceptions so you can count 500 exceptions in final report.
        if (typeName1 == api.Response.GetType().Name || typeName2 == api.Response.GetType().Name)
        {
            //var exception = api.Response as ApiException;
            //var simplifiedException = new { exception?.StatusCode, exception?.GetType().Name };

            // Get the message and replace newlines with a space or remove them
            string message = ((dynamic)api?.Response)?.Message;
            string? scrubedAndFixedMessage = ScrubAndFixMessage(message);


            // Extract the status code, message, and type name from the response
            var simplifiedException = new
            {
                ((dynamic)api?.Response)?.StatusCode,
                Message = scrubedAndFixedMessage,
                TypeName = api?.Response?.GetType()?.Name,
                //InnerException = ((dynamic)api?.Response)?.InnerException?.Message,
                //StackTrace = ((dynamic)api?.Response)?.StackTrace,
                //Timestamp = DateTime.UtcNow,
            };

            return simplifiedException;
        }

        var res = api.Response switch
        {
            object => api.Response,
            _ => throw new Exception("cant parse")
        };

        return res;
    }

    static string ScrubAndFixMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        // Remove newlines
        string cleanedMessage = message.Replace("\n", " ").Replace("\r", " ").Replace("\r\n", " ");

        // Regular expression to find the "traceId" field and remove it
        string traceIdPattern = "\"traceId\":\"[^\"]*\",?";
        cleanedMessage = Regex.Replace(cleanedMessage, traceIdPattern, "", RegexOptions.IgnoreCase);

        // Regular expression to find date/timestamp fields and remove them
        string datePattern = @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z";
        cleanedMessage = Regex.Replace(cleanedMessage, datePattern, "REMOVED_DATE", RegexOptions.IgnoreCase);

        // Trim any trailing spaces or commas
        return cleanedMessage.Trim(new char[] { ' ', ',' });
    }

    //private void RecreateFixture(TestApplication _factory, HttpClient _httpClient)
    //{
    //    //_factory.Dispose();
    //    //_factory = new TestApplication(); //This is .net specific fuzzing scenario.
    //}

    public string CreateSanitizedNamespace()
    {
        var name = "GeneratedClientMyNamespace";
        var guid = "RuntimeCompilationBuild" + new Random(_seed).Next();

        // Define a pattern for illegal characters
        var illegalCharactersPattern = @"[-:.\s_~!@#$%^&*()+=|\\{}[\]<>?/`';]";

        // Remove illegal characters using regex
        var sanitizedName = Regex.Replace(guid, illegalCharactersPattern, string.Empty);

        return name + sanitizedName;
    }

    private async Task<object> GetOpenApiClientInstanceDynamically(string fullOpenApiPath, HttpClient HttpClientForOpenApiDownload)
    {
        //Validate fullOpenApiPath url by pinging it, throw exception that url is not reachable or its not web api not running.

        var swaggerContent = string.Empty;
        try
        {
            //// Step 1: Get the Swagger JSON
            var response = await HttpClientForOpenApiDownload.GetAsync(fullOpenApiPath);
            swaggerContent = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to reach the OpenAPI URL: {fullOpenApiPath}. Please ensure the URL is correct and the API is running.", ex);
        }

        // Step 2: Determine the content type based on the extension and parse accordingly
        OpenApiDocument document = null;

        if (fullOpenApiPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            // Parse JSON
            document = await OpenApiDocument.FromJsonAsync(swaggerContent);
        }
        else if (fullOpenApiPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || fullOpenApiPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {

            var oaDoc = await OpenApiYamlDocument.FromYamlAsync(swaggerContent);
            document = oaDoc;
        }
        else
        {
            throw new NotSupportedException("Swagger file format is not supported. Please provide a .json or .yaml file.");
        }

        // Remove AdditionalProperties. 
        foreach (var kvp in document.Components.Schemas)
        {
            kvp.Value.ActualSchema.AllowAdditionalProperties = false;
        }


        var uniqueNamespace = CreateSanitizedNamespace();

        // Step 3: Configure CSharpClientGeneratorSettings
        var settings = new CSharpClientGeneratorSettings
        {
            ClassName = "GeneratedClient",
            ExposeJsonSerializerSettings = true,
            UseBaseUrl = true,
            OperationNameGenerator = new CustomOperationNameGenerator(),
            AdditionalNamespaceUsages = new[] {
                 "FuseDrill.Core",
                 "System",
                 },
            CSharpGeneratorSettings =
            {


                Namespace = uniqueNamespace,
                HandleReferences = true,
                GenerateNullableReferenceTypes = true, // Enable nullable reference types
                TypeNameGenerator = new CustomTypeNameGenerator(),
                PropertyNameGenerator = new CustomPropertyNameGenerator(),
                EnumNameGenerator = new CustomEnumNameGenerator(),
                ExcludedTypeNames = new[]
                {
                    "GroupSchema",
                    "DateOnly",
                    "TimeOnly"
                }

            }
        };

        // Set the custom contract resolver
        //settings.JsonSerializerSettings.ContractResolver = new YamlAliasContractResolver();

        // Step 4: Generate the C# code
        var generator = new CSharpClientGenerator(document, settings);
        var generatedCode = generator.GenerateFile();

        var cleanFile = string.Join(Environment.NewLine, ToLines(generatedCode))
            // Removes AdditionalProperties property from types as they are required to derive from IDictionary<string, object> for deserialization to work properly
            .Replace($"        private System.Collections.Generic.IDictionary<string, object>? _additionalProperties;", string.Empty)
            .Replace($"        private System.Collections.Generic.IDictionary<string, object> _additionalProperties;", string.Empty)
            .Replace($"        [Newtonsoft.Json.JsonExtensionData]{Environment.NewLine}        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties{Environment.NewLine}        {{{Environment.NewLine}            get {{ return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }}{Environment.NewLine}            set {{ _additionalProperties = value; }}{Environment.NewLine}        }}{Environment.NewLine}", string.Empty)
            // Fixes stray blank lines from the C# generator
            .Replace($"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}", Environment.NewLine)
            .Replace($"{Environment.NewLine}{Environment.NewLine}    }}", $"{Environment.NewLine}    }}")
            // Weird generation issue workaround
            .Replace($"{uniqueNamespace}.bool.True", "true");

        var cleanedGeneratedCode = cleanFile;

        // Step 5: Compile the generated code with Roslyn
        var syntaxTree = CSharpSyntaxTree.ParseText(cleanedGeneratedCode);
        var assemblyName = "DynamicGeneratedClient";
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var aditionalReferences = MetadataReference.CreateFromFile(typeof(AllowedValuesAttribute).Assembly.Location);

        references.Add(aditionalReferences);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        EmitResult result = compilation.Emit(ms); // Slow part cost 1.7 seconds to compile every time.

        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException($"Compilation failed: {errors}");
        }

        // Step 6: Load the compiled assembly and create an instance of the client
        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        var clientType = assembly.GetType($"{uniqueNamespace}.GeneratedClient");

        if (clientType == null)
        {
            throw new InvalidOperationException("Generated client type not found.");
        }

        // Step 7: Determine the constructor parameters
        var constructorInfo = clientType.GetConstructors()
                                         .OrderByDescending(c => c.GetParameters().Length)
                                         .FirstOrDefault();

        if (constructorInfo == null)
        {
            throw new InvalidOperationException("No valid constructor found for the client type.");
        }

        // Step 8: Create an instance of the GeneratedClient
        object clientInstance;
        var parameters = constructorInfo.GetParameters();

        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(HttpClient))
        {
            // Only HttpClient is required
            clientInstance = Activator.CreateInstance(clientType, _httpClient);
            ((dynamic)clientInstance).BaseUrl = _baseurl;
        }
        else if (parameters.Length == 2 &&
                 parameters[0].ParameterType == typeof(string) &&
                 parameters[1].ParameterType == typeof(HttpClient))
        {
            // Both baseAddress and HttpClient are required
            clientInstance = Activator.CreateInstance(clientType, _baseurl, _httpClient);
            ((dynamic)clientInstance).BaseUrl = _baseurl;
        }
        else
        {
            throw new InvalidOperationException("Unsupported constructor parameters for the client type.");
        }

        return clientInstance;
    }

    public static IEnumerable<string> ToLines(string value, bool removeEmptyLines = false)
    {
        using var sr = new StringReader(value);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (removeEmptyLines && string.IsNullOrWhiteSpace(line))
                continue;
            yield return line;
        }
    }

    //private object RecreateSwaggerClientInstanceTest()
    //{
    //    var assembly = Assembly.GetExecutingAssembly();

    //    var clientType = assembly.GetType($"{uniqueNamespace}.GeneratedClient");

    //    if (clientType == null)
    //    {
    //        throw new InvalidOperationException("Generated client type not found.");
    //    }

    //    var clientInstance = Activator.CreateInstance(clientType, _httpClient.BaseAddress, _httpClient);
    //    return clientInstance;
    //}

    private static async Task<ApiCall> DoApiCall(object ClientInstance, ApiCall api) 
    {
        Debug.Assert(api?.MethodName is not null, "Method name should be always filled.");

        try
        {
            var input = new object[] { };
            input = handleMultipleParameter(api);

            Task task = null;
            try
            {
                // Dynamically invoke the method on the instance
                task = (Task)api.Method.MethodForCall.Invoke(ClientInstance, input);
                await task;
            }
            catch (Exception ex)
            {

                //resolving api client exceptions dynamically, Dont have actual types during build time
                var typeName1 = "ApiException`1";
                var typeName2 = "ApiException";
                if (typeName1 == ex.GetType().Name || typeName2 == ex.GetType().Name)
                {
                    api.Response = ex;
                    return api;
                }

                // exception is in fuzzer source code;
                //Todo: add logs
                throw;
            }

            // Retrieve the result from the Task
            var resultProperty = task.GetType().GetProperty("Result");
            var result = resultProperty?.GetValue(task);

            api.Response = result;

        }
        catch (Exception e)
        {
            // add api call object to exception data bag
            e.Data.Add("ApiCall", api);

            throw;
            //throw new Exception("Cant determine what to do next", e);
        }

        return api;
    }

    private static object[]? handleMultipleParameter(ApiCall api)
    {
        var parameterArray = api.RequestParameters.Select(item => item.Value).ToArray();

        return parameterArray;
    }

    public void ResetCoverage()
    {
        _coverageTracker = new CoverageTracker();
        _lastCoverageReport = null;
    }

    public CoverageReport GetCoverageReport()
    {
        if (_lastCoverageReport == null)
        {
            _lastCoverageReport = _coverageTracker?.GenerateReport() ?? new CoverageReport();
        }
        return _lastCoverageReport;
    }

    public async Task<FuzzerTests> TestWholeApiWithCoverageGuidance(FuzzingOptions options)
    {
        _coverageTracker = new CoverageTracker();
        var logger = options.Logger ?? (_ => { });
        logger("Starting coverage-guided fuzzing...");

        var genericClientInstance = _openApiClientClassInstance ?? await GetOpenApiClientInstanceDynamically(_swaggerPath, _httpClientForSwaggerDownload);
        var apiClientAsData = new ApiShapeData(genericClientInstance);
        var dataGenerationHelper = new DataGenerationHelper(_seed);
        var testSuiteGen = dataGenerationHelper.CreateApiMethodPermutationsTestSuite(apiClientAsData);

        var edgeCaseGenerator = new SchemaEdgeCaseGenerator();
        var combinatorialGenerator = new CombinatorialGenerator();
        var corpusMinimizer = new CorpusMinimizer();

        var fuzzerTests = new FuzzerTests
        {
            Seed = _seed
        };

        var metrics = new FuzzingSessionMetrics
        {
            TotalApiCalls = 0,
            UniqueInputsGenerated = 0,
            EdgeCasesGenerated = 0,
            CombinationsGenerated = 0,
            MutationsApplied = 0,
            ExceptionsEncountered = 0,
            DiscoveredExceptionTypes = new List<string>()
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var testSuite in testSuiteGen)
        {
            if (options.Filter != null)
            {
                testSuite.ApiCalls = testSuite.ApiCalls.Where(options.Filter).ToList();
            }

            var originalApiCalls = testSuite.ApiCalls.ToList();
            metrics.TotalApiCalls += originalApiCalls.Count;
            metrics.UniqueInputsGenerated += originalApiCalls.Count;

            var apiCalls = new List<ApiCall>(originalApiCalls);

            if (options.EnableEdgeCaseGeneration)
            {
                var enhancedWithEdgeCases = EnhanceWithEdgeCases(apiCalls, edgeCaseGenerator).ToList();
                metrics.EdgeCasesGenerated += enhancedWithEdgeCases.Count - apiCalls.Count;
                apiCalls = enhancedWithEdgeCases;
            }

            if (options.EnableCombinatorialTesting)
            {
                var enhancedWithCombos = EnhanceWithCombinations(apiCalls, combinatorialGenerator, logger).ToList();
                metrics.CombinationsGenerated += enhancedWithCombos.Count - apiCalls.Count;
                apiCalls = enhancedWithCombos;
            }

            metrics.TotalApiCalls += apiCalls.Count;

            if (_callEndpoints)
            {
                foreach (var apiCall in apiCalls)
                {
                    var trackedCall = _coverageTracker.StartTracking(apiCall);
                    var callStart = DateTime.UtcNow;
                    try
                    {
                        var api = await DoApiCall(genericClientInstance, apiCall);
                        apiCall.Response = ResolveResponse(api);
                        _coverageTracker.CompleteTracking(trackedCall, apiCall.Response);
                    }
                    catch (Exception ex)
                    {
                        metrics.ExceptionsEncountered++;
                        var exceptionType = ex.GetType().Name;
                        if (!metrics.DiscoveredExceptionTypes.Contains(exceptionType))
                        {
                            metrics.DiscoveredExceptionTypes.Add(exceptionType);
                        }
                        _coverageTracker.CompleteTracking(trackedCall, ex);
                    }
                    apiCall.Method = null;
                }
            }

            testSuite.ApiCalls = apiCalls;
            fuzzerTests.TestSuites.Add(testSuite);
        }

        if (options.MinimizeInputs && _callEndpoints)
        {
            logger("Minimizing corpus...");
            var beforeCount = fuzzerTests.TestSuites.Sum(s => s.ApiCalls.Count);
            metrics.InputsBeforeMinimization = beforeCount;

            var allCalls = fuzzerTests.TestSuites.SelectMany(s => s.ApiCalls).ToList();
            var minimizedCalls = corpusMinimizer.Minimize(allCalls, logger);
            
            metrics.InputsAfterMinimization = minimizedCalls.Count;
            metrics.UniqueBehaviors = minimizedCalls.Count;
            metrics.MinimizationReductionPercent = beforeCount > 0 
                ? Math.Round((double)(beforeCount - minimizedCalls.Count) / beforeCount * 100, 1) 
                : 0;

            var minimizedBySuite = new Dictionary<int, List<ApiCall>>();
            foreach (var call in minimizedCalls)
            {
                var suiteId = call.ApiCallOrderId / 10000;
                if (!minimizedBySuite.ContainsKey(suiteId))
                {
                    minimizedBySuite[suiteId] = new List<ApiCall>();
                }
                minimizedBySuite[suiteId].Add(call);
            }

            foreach (var testSuite in fuzzerTests.TestSuites)
            {
                var suiteId = testSuite.TestSuiteOrderId;
                if (minimizedBySuite.TryGetValue(suiteId, out var calls))
                {
                    testSuite.ApiCalls = calls;
                }
            }
        }

        sw.Stop();
        metrics.TotalDuration = sw.Elapsed;
        metrics.UniqueExceptions = metrics.DiscoveredExceptionTypes.Count;

        _lastCoverageReport = _coverageTracker.GenerateReport();
        _lastCoverageReport.FuzzingMetrics = metrics;

        logger($"Coverage-guided fuzzing complete in {sw.Elapsed.TotalSeconds:F2}s");
        logger($"Total API calls: {metrics.TotalApiCalls}");
        logger($"Edge cases generated: {metrics.EdgeCasesGenerated}");
        logger($"Combinations generated: {metrics.CombinationsGenerated}");
        logger($"Exceptions encountered: {metrics.ExceptionsEncountered}");
        
        if (options.MinimizeInputs)
        {
            logger($"Minimization: {metrics.InputsBeforeMinimization} -> {metrics.InputsAfterMinimization} ({metrics.MinimizationReductionPercent}% reduction)");
            logger($"Unique behaviors: {metrics.UniqueBehaviors}");
        }

        fuzzerTests.TestSuites.ForEach(suite => 
        {
            suite.ApiCalls = suite.ApiCalls.OrderBy(item => item.ApiCallOrderId).ThenBy(item => item.MethodName).ToList();
        });
        fuzzerTests.TestSuites.OrderBy(item => item.TestSuiteOrderId);

        return fuzzerTests;
    }

    private IEnumerable<ApiCall> EnhanceWithEdgeCases(IEnumerable<ApiCall> apiCalls, SchemaEdgeCaseGenerator edgeCaseGenerator)
    {
        foreach (var apiCall in apiCalls)
        {
            yield return apiCall;

            foreach (var edgeCase in edgeCaseGenerator.GenerateEdgeCases(apiCall))
            {
                yield return edgeCase;
            }
        }
    }

    private IEnumerable<ApiCall> EnhanceWithCombinations(IEnumerable<ApiCall> apiCalls, CombinatorialGenerator combinatorialGenerator, Action<string> logger)
    {
        foreach (var apiCall in apiCalls)
        {
            yield return apiCall;

            var parameterValues = new Dictionary<string, List<object>>();
            foreach (var param in apiCall.RequestParameters)
            {
                parameterValues[param.Name] = GenerateSampleValues(param.Type);
            }

            var parameters = apiCall.RequestParameters.Select(p => new Parameter { Name = p.Name, Type = p.Type }).ToList();
            var combinations = combinatorialGenerator.GenerateTwise(parameters, parameterValues, 2);

            foreach (var combo in combinations)
            {
                var comboCall = new ApiCall
                {
                    ApiCallOrderId = apiCall.ApiCallOrderId + 10000,
                    MethodName = apiCall.MethodName,
                    HttpMethod = apiCall.HttpMethod,
                    RequestParameters = apiCall.RequestParameters.Select(p => new ParameterValue
                    {
                        Name = p.Name,
                        Type = p.Type,
                        Value = combo.TryGetValue(p.Name, out var val) ? val : p.Value
                    }).ToList(),
                    Response = null!
                };
                yield return comboCall;
            }
        }
    }

    private List<object> GenerateSampleValues(Type type)
    {
        var values = new List<object>();
        
        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            values.AddRange(new object[] { 0, 1, -1, 100 });
        }
        else if (type == typeof(double) || type == typeof(decimal) || type == typeof(float))
        {
            values.AddRange(new object[] { 0.0, 1.0, -1.0, 100.5 });
        }
        else if (type == typeof(string))
        {
            values.AddRange(new object[] { "", "test", "TEST123" });
        }
        else if (type == typeof(bool))
        {
            values.AddRange(new object[] { true, false });
        }
        else
        {
            values.Add(null!);
        }
        
        return values;
    }

    private ApiCall CloneApiCall(ApiCall original)
    {
        return new ApiCall
        {
            ApiCallOrderId = original.ApiCallOrderId,
            MethodName = original.MethodName,
            HttpMethod = original.HttpMethod,
            RequestParameters = original.RequestParameters.Select(p => new ParameterValue
            {
                Name = p.Name,
                Value = p.Value,
                Type = p.Type
            }).ToList(),
            Response = null!
        };
    }

    private ApiCall MutateApiCall(ApiCall apiCall, int mutationCount)
    {
        var random = new Random(_seed ^ (int)DateTime.UtcNow.Ticks);
        var mutationEngine = new MutationEngine(_seed);

        var seed = new InputSeed
        {
            Values = apiCall.RequestParameters.ToDictionary(p => p.Name, p => p.Value ?? "")
        };

        var mutatedSeeds = mutationEngine.Mutate(seed, mutationCount);
        var firstMutated = mutatedSeeds.FirstOrDefault();

        if (firstMutated == null)
        {
            return CloneApiCall(apiCall);
        }

        return new ApiCall
        {
            ApiCallOrderId = apiCall.ApiCallOrderId + 5000,
            MethodName = apiCall.MethodName,
            HttpMethod = apiCall.HttpMethod,
            RequestParameters = apiCall.RequestParameters.Select(p => new ParameterValue
            {
                Name = p.Name,
                Type = p.Type,
                Value = firstMutated.Values.TryGetValue(p.Name, out var val) ? val : p.Value
            }).ToList(),
            Response = null!
        };
    }

    private async Task ExecuteWithCoverageGuidance(object clientInstance, FuzzingOptions options)
    {
        var logger = options.Logger ?? (_ => { });
        var population = new List<ApiCall>();
        var dataGenerationHelper = new DataGenerationHelper(_seed);
        var edgeCaseGenerator = new SchemaEdgeCaseGenerator();
        var combinatorialGenerator = new CombinatorialGenerator(_seed);
        var mutationEngine = new MutationEngine(_seed);

        var sw = Stopwatch.StartNew();
        var iteration = 0;

        while (iteration < options.MaxIterations && sw.Elapsed.TotalSeconds < options.MaxDurationSeconds)
        {
            iteration++;

            var candidates = new List<ApiCall>();

            if (options.EnableEdgeCaseGeneration)
            {
                foreach (var apiCall in population)
                {
                    candidates.AddRange(edgeCaseGenerator.GenerateEdgeCases(apiCall));
                }
            }

            if (options.EnableMutation && population.Count > 0)
            {
                foreach (var apiCall in population.Take(population.Count / 2))
                {
                    candidates.Add(MutateApiCall(apiCall, options.MutationCount));
                }
            }

            foreach (var candidate in candidates)
            {
                var trackedCall = _coverageTracker.StartTracking(candidate);
                try
                {
                    var api = await DoApiCall(clientInstance, candidate);
                    _coverageTracker.CompleteTracking(trackedCall, api.Response);
                }
                catch (Exception ex)
                {
                    _coverageTracker.CompleteTracking(trackedCall, ex);
                }
            }

            population.AddRange(candidates.Where(c => !population.Contains(c)));

            var report = _coverageTracker.GenerateReport();
            if (report.TotalCoveragePercentage >= options.TargetCoverage)
            {
                logger($"Target coverage reached at iteration {iteration}");
                break;
            }

            if (iteration % 100 == 0)
            {
                logger($"Iteration {iteration}, Coverage: {report.TotalCoveragePercentage}%, Unique Coverage: {report.UniqueCoveragePoints}");
            }
        }

        sw.Stop();
        logger($"Fuzzing completed in {sw.Elapsed.TotalSeconds}s, {iteration} iterations");
    }
}

public class CoverageGuidedFuzzer
{
    private readonly ApiFuzzer _apiFuzzer;
    private readonly FuzzingOptions _options;

    public CoverageGuidedFuzzer(ApiFuzzer apiFuzzer, FuzzingOptions? options = null)
    {
        _apiFuzzer = apiFuzzer;
        _options = options ?? new FuzzingOptions();
    }

    public async Task<FuzzerTests> FuzzAsync()
    {
        return await _apiFuzzer.TestWholeApiWithCoverageGuidance(_options);
    }

    public CoverageReport GetCoverageReport()
    {
        return _apiFuzzer.GetCoverageReport();
    }

    public void Reset()
    {
        _apiFuzzer.ResetCoverage();
    }
}

public class CustomPropertyNameGenerator : IPropertyNameGenerator
{
    public string Generate(JsonSchemaProperty property)
    {
        // Define a pattern for illegal characters
        var illegalCharactersPattern = @"[-:.\s_~!@#$%^&*()+=|\\{}[\]<>?/`';]";

        // Remove illegal characters using regex
        var sanitizedPropertyName = Regex.Replace(property.Name, illegalCharactersPattern, string.Empty);

        // Ensure the resulting name conforms to PascalCase (UpperCamelCase) convention
        var propertyName = ConversionUtilities.ConvertToUpperCamelCase(sanitizedPropertyName, true);

        return propertyName;
    }
}

public class CustomTypeNameGenerator : DefaultTypeNameGenerator
{
    // Class names that conflict with project class names
    private static readonly Dictionary<string, string> RenameMap = new Dictionary<string, string>
        {
            { "HttpHeader", "HttpResponseHeader" },
            { "Parameter", "RequestParameter" },
            { "Request", "ServiceRequest" },
            { "Response", "ServiceResponse" },
            { "SerializationFormat", "SerializationFormatMetadata" }
        };

    public override string Generate(JsonSchema schema, string typeNameHint, IEnumerable<string> reservedTypeNames)
    {
        if (typeNameHint == null)
        {
            return base.Generate(schema, typeNameHint, reservedTypeNames);
        }

        if (RenameMap.ContainsKey(typeNameHint))
        {
            typeNameHint = RenameMap[typeNameHint];
        }

        typeNameHint = typeNameHint.Replace("-", "");

        // Define a pattern for illegal characters
        var illegalCharactersPattern = @"[-:.\s_~!@#$%^&*()+=|\\{}[\]<>?/`';]";

        // Remove illegal characters using regex
        var sanitizedTypeNameHintName = Regex.Replace(typeNameHint, illegalCharactersPattern, string.Empty);

        return base.Generate(schema, sanitizedTypeNameHintName, reservedTypeNames);
    }
}

public class CustomEnumNameGenerator : IEnumNameGenerator
{
    private readonly DefaultEnumNameGenerator _defaultEnumNameGenerator = new DefaultEnumNameGenerator();

    public string Generate(int index, string name, object value, JsonSchema schema) =>
        _defaultEnumNameGenerator.Generate(
            index,
            name.Equals("+") ? "plus" : name.Equals("-") ? "minus" : name,
            value,
            schema);
}

public class CustomOperationNameGenerator : IOperationNameGenerator
{
    public bool SupportsMultipleClients { get; } = false;

    public string GetClientName(OpenApiDocument document, string settingsClassName, string clientVariableName)
    {
        return settingsClassName;
    }

    public string GetClientName(OpenApiDocument document, string settingsClassName, string clientVariableName, OpenApiOperation operation)
    {
        return settingsClassName;
    }

    public string GetOperationName(OpenApiDocument document, string path, string httpMethod, OpenApiOperation operation)
    {
        return operation.OperationId ?? $"{httpMethod}_{path.Replace("/", "_").TrimStart('_')}";
    }
}
