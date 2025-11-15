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

    private async Task<object> GetOpenApiClientInstanceDynamically(string fullOpenApiPath, HttpClient HttpClientForOpenApiDownalod)
    {
        //// Step 1: Get the Swagger JSON
        ///
        var response = await HttpClientForOpenApiDownalod.GetAsync(fullOpenApiPath);
        var swaggerContent = await response.Content.ReadAsStringAsync();

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

            var value = new object();
            try
            {
                value = api.Method.MethodForCall.Invoke(ClientInstance, input);

                //only allow simple values like string, bool, int,

                if (value is null)
                {
                    api.Response = "Empty response (void) or (null)";
                }
                else if (value is string stringValue)
                {
                    api.Response = stringValue;
                }
                else if (value is bool boolValue)
                {
                    api.Response = boolValue;
                }
                else if (value is int intvalue)
                {
                    api.Response = intvalue;
                }
                else
                {
                    string serialized = "Not serializable";
                    try
                    {
                        var type = value?.GetType();

                        if (typeof(Task).IsAssignableFrom(type))
                        {
                            var task = (Task)value;
                            await task.ConfigureAwait(false);

                            if (type.IsGenericType)
                            {
                                // Extract Result property for Task<T>
                                var resultProperty = type.GetProperty("Result");
                                value = resultProperty.GetValue(task);
                            }
                            else
                            {
                                // For non-generic Task, no result
                                value = null;
                            }
                        }

                        serialized = System.Text.Json.JsonSerializer.Serialize(value);  //Todo Use argon serializer, This Still can fail whole serialization, because verify uses Argon serializer.
                        api.Response = (object)value;
                    }
                    catch
                    {
                        api.Response = serialized;
                    }
                }

            }
            catch (Exception ex)
            {
                api.Response = ex?.Message + " | " + ex?.InnerException?.Message;
            }

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
            // Fixes + and - enum values that cannot be generated into C# enum names
            name.Equals("+") ? "plus" : name.Equals("-") ? "minus" : name,
            value,
            schema);
}

public class CustomOperationNameGenerator : IOperationNameGenerator
{
    // Default implementation for SupportsMultipleClients (this can return true or false based on your needs)
    public bool SupportsMultipleClients => false; // Adjust as needed for your case

    // Default implementation for GetClientName (you can leave it empty or return a default value)
    public string GetClientName(OpenApiDocument document, string path, string httpMethod, OpenApiOperation operation)
    {
        // Return default value or handle custom logic here
        return string.Empty; // Can be empty if not needed
    }

    // Default implementation for GetOperationName (so it doesn't throw exception)
    public string GetOperationName(OpenApiDocument document, string path, string httpMethod, OpenApiOperation operation)
    {
        return operation.OperationId + $"_http_{httpMethod}_"; // Or return some meaningful default value
    }
}
