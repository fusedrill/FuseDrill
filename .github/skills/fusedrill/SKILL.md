---
name: fusedrill
description: >
  FuseDrill - API fuzzing and simulation testing tool. Use when the user wants to
  "fuzz API", "test API contract", "find API breaking changes", "run FuseDrill",
  "check API stability", "simulate API testing", or similar fuzzing/simulation 
  testing requests. Fuses OpenAPI specs to generate random test cases and verify responses
  match expected patterns.
---

# FuseDrill - API Fuzzing Skill

FuseDrill is a tool for **fuzzing** and **simulation testing** of **OpenAPIs** using snapshots. It helps identify API contract changes, bugs, and breaking changes before releasing updates.

## When to Use This Skill

Use FuseDrill when the user:
- Wants to "fuzz my API"
- Wants to "test API stability" or "check API contract"
- Asks to "find breaking changes" in their API
- Wants to "run FuseDrill" or "run fuzzing test"
- Needs to verify an API works as expected
- Wants to add API fuzzing tests to their codebase
- Asks about API simulation testing

## Prerequisites

Before starting, gather:

1. **API Base URL** - The base URL of the API to test (e.g., `http://localhost:8080/`)
2. **OpenAPI Spec URL** - URL to the OpenAPI/Swagger specification (e.g., `http://localhost:8080/swagger/v1/swagger.json`)
3. **Optional: Authorization** - Bearer token or Basic credentials if the API requires authentication

## Tools / Workflows

### Tool 1: First Pass Fuzzing

**Purpose**: Initial API discovery - figure out the API structure, endpoints, and response shapes.

**When to use**: First time testing an API, when user wants to "just run fuzzing", or as a starting point.

**Steps**:

1. Ask the user for:
   - Base URL of the API
   - OpenAPI spec URL (can be swagger endpoint)
   - Optional: Authorization header (e.g., `Bearer <token>`)

2. Start the API server (if local):
   ```bash
   dotnet run --urls "http://localhost:5184"
   ```

3. Run the first pass fuzzing:
   - **Option A - Using Core library directly** (recommended for skill):
   ```csharp
   var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5184/") };
   var fuzzer = new ApiFuzzer(httpClient, "http://localhost:5184/swagger/v1/swagger.json", seed: 12345);
   var results = await fuzzer.TestWholeApi();
   ```
   - **Option B - Using xUnit test**:
   ```csharp
   var fuzzer = new ApiFuzzerWithVerifier<Program>();
   await fuzzer.TestWholeApi();
   ```
   - **Option C - Using CLI** (requires GitHub token):
   ```bash
   FUSEDRILL_BASE_ADDRESS="http://localhost:5184/" \
   FUSEDRILL_OPENAPI_URL="http://localhost:5184/swagger/v1/swagger.json" \
   dotnet run --project src/FuseDrill.Cli/FuseDrill.Cli.csproj
   ```

4. Present the results clearly:
   - Show a table of endpoints and their status
   - Include seed value (for reproducibility)
   - Show test suite count
   - Note any expected vs unexpected failures

5. **IMPORTANT - Ask the user what to do next** (must ask explicitly):
   > "Following the fuzzing test, what would you like to do now?"
   > 
   > 1. **Test specific endpoints** - Focus on one endpoint with custom parameters
   > 2. **Add a fuzzing test to your codebase** - Get C# test template to commit
   > 3. **Run all except list** - Test all endpoints except certain ones (like DELETE)
   > 4. **Generate test template** - Get code template for remote/CI fuzzing
   > 5. **Something else** - Let me know your preference

---

### Tool 2: Fuzz All Endpoints Except List

**Purpose**: Fuzz all API endpoints while excluding certain ones that might break other tests (like DELETE, ChangePassword, UpdateUser).

**When to use**: When user wants to fuzz the entire API but skip destructive or state-changing endpoints.

**Steps**:

1. Ask for:
   - Base URL and OpenAPI spec URL
   - List of endpoint names to exclude (e.g., `["Delete", "ChangePassword", "UpdateUser"]`)
   - Optional: Authorization header

2. Run fuzzing with exclusion filter

3. Return results showing tested endpoints and any failures

4. Ask if user wants to test the excluded endpoints separately

---

### Tool 3: Specific Endpoint Fuzzing

**Purpose**: Test a single endpoint with custom request parameters.

**When to use**: When user wants to focus on testing one specific endpoint deeply after seeing first pass results.

**Steps**:

1. Ask for:
   - The endpoint name to test (from first pass results)
   - Base URL and OpenAPI spec URL
   - Request parameters to use (from FirstPassFuzzing results)
   - Optional: Authorization header

2. Run fuzzing for that specific endpoint

3. Return detailed results for that endpoint

4. **MUST ASK**: "What would you like to do next?"

---

#### Example: Customize After First Pass Fuzzing

After running first pass fuzzing, you might see results like this:

```
=== First Pass Fuzzing Results ===
| Endpoint      | HTTP | Status   |
|--------------|------|----------|
| /Pets        | GET | ✅ Success |
| /Pets        | POST | ✅ Success |
| /Pets/{id}  | GET | ⚠️ 404 Not Found |

Questions:
- Why is GET /Pets/{id} returning 404?
- Does the API require the pet to exist first?
```

**Step 1: Ask user what to test**

User says: "I want to test POST /Pets first to create a pet, then GET /Pets/{id}"

**Step 2: Run specific endpoint fuzzing with custom parameters**

```csharp
using System;
using System.Net.Http;
using System.Text.Json;
using FuseDrill.Core;

// Setup
var httpClient = new HttpClient 
{ 
    BaseAddress = new Uri("http://localhost:5184/") 
};

// Step 1: First create a pet via POST
var createFuzzer = new ApiFuzzer(httpClient, "http://localhost:5184/swagger/v1/swagger.json", seed: 12345);
var createResults = await createFuzzer.TestWholeApi(apiCall => apiCall.MethodName.EndsWith("PetsPOST"));

// Extract the pet ID from the POST response
var petId = createResults.TestSuites[0].ApiCalls[0].Response.Id;
Console.WriteLine($"Created pet with ID: {petId}");

// Step 2: Now test GET /Pets/{id} with the specific ID
var getFuzzer = new ApiFuzzer(httpClient, "http://localhost:5184/swagger/v1/swagger.json", seed: 12345);
var getResults = await getFuzzer.TestWholeApi(apiCall => 
{
    if (apiCall.MethodName.EndsWith("PetsGET"))
    {
        // Override the id parameter with the created pet's ID
        apiCall.RequestParameters = new List<ParameterValue>
        {
            new ParameterValue { Name = "id", Type = "Int32", Value = petId }
        };
        return true;
    }
    return false;
});

// Results
foreach (var suite in getResults.TestSuites)
{
    foreach (var call in suite.ApiCalls)
    {
        Console.WriteLine($"{call.MethodName}: {call.HttpMethod}");
        Console.WriteLine($"  Response: {JsonSerializer.Serialize(call.Response)}");
    }
}
```

**Step 3: Results show the issue**

```
Method: PetsGET_http_get_Async
Response: {
  "id": 1,
  "name": "Fluffy",
  "breed": "RandomString774",
  "petType": 1
}
```

**The fix**: The API works correctly - you just need to create the pet first before trying to GET it by ID!

---

#### Alternative: Using the Filter Function Directly

```csharp
// Filter to only test Pets endpoints with custom override
var results = await fuzzer.TestWholeApi(apiCall =>
{
    // Only test endpoints containing "Pets"
    var isPetsEndpoint = apiCall.MethodName.Contains("Pets");
    
    // Customize request for GET /Pets/{id}
    if (isPetsEndpoint && apiCall.HttpMethod == "get" && apiCall.MethodName.Contains("GET"))
    {
        // Override with specific ID
        apiCall.RequestParameters = new List<ParameterValue>
        {
            new ParameterValue { Name = "id", Type = "Int32", Value = 42 }
        };
    }
    
    return isPetsEndpoint;
});
```

**Key points**:
- Use `apiCall.MethodName.EndsWith("EndpointName")` to filter
- Use `apiCall.RequestParameters` to override values
- Chain multiple conditions with `&&` and `||`
- Test the response to understand API behavior

---

### Tool 4: Generate C# Test Template

**Purpose**: Generate reusable C# test code that can be added to the user's codebase.

**When to use**: When user wants to "add a fuzzing test", "write a test", or "add to my project".

**Steps**:

1. Ask: Single endpoint or all endpoints?

2. Generate the appropriate C# template

3. Explain how to integrate it into their project

---

## Test Examples

### Example 1: Basic xUnit Test

From `tests/FuseDrill.Tests/DrillerTests.cs`:

```csharp
using FuseDrill;

[Fact]
public async Task TestMinimumConfiguration()
{
    // If using top-level statements in Web API, add:
    // public partial class Program { }

    var fuzzer = new ApiFuzzerWithVerifier<Program>();
    await fuzzer.TestWholeApi();
}
```

---

### Example 2: Remote Fuzzing with HttpClient

From `tests/FuseDrill.Tests/DrillerTests.cs`:

```csharp
[Fact]
public async Task TestRemoteFuzzing()
{
    var apiProjectFileName = "TestApi.csproj";
    
    var apiProcessManager = new ApiProcessManager();
    await apiProcessManager.DotnetRun(apiProjectFileName);

    var apiUrl = "http://localhost:5184/";
    var swaggerPath = "http://localhost:5184/swagger/v1/swagger.json";

    var httpClient = new HttpClient
    {
        BaseAddress = new Uri(apiUrl)
    };

    var fuzzer = new ApiFuzzerWithVerifier(httpClient, swaggerPath);
    await fuzzer.TestWholeApi();

    await apiProcessManager.DisposeAsync();
}
```

---

### Example 3: Test External API (apis.guru)

From `tests/FuseDrill.Tests/RemoteFuzzingTests.cs`:

```csharp
[Fact]
public async Task ApiGuruYamlTest()
{
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri("https://api.apis.guru/v2")
    };

    var tester = new ApiFuzzerWithVerifier(
        httpClient, 
        "https://api.apis.guru/v2/openapi.yaml"
    );
    await tester.TestWholeApi();
}
```

---

### Example 4: Filter by HTTP Method

From `tests/FuseDrill.Tests/RemoteFuzzingTests.cs`:

```csharp
[Fact]
public async Task ExoscaleTest()
{
    var key = Environment.GetEnvironmentVariable("exoscale");
    var handler = new ExoscaleAuthHandler("YOUR_KEY", key)
    {
        InnerHandler = new HttpClientHandler()
    };

    var httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri("https://api-ch-gva-2.exoscale.com/v2")
    };

    var tester = new ApiFuzzerWithVerifier(
        httpClient, 
        "https://openapi-v2.exoscale.com/source.json"
    );
    await tester.TestWholeApi(apiCall => apiCall.HttpMethod == "get");
}
```

---

### Example 5: Custom TestClient (Generated OpenAPI Client)

From `tests/FuseDrill.Tests/DrillerTests.cs`:

```csharp
[Fact]
public async Task LibraryConsumerProvidesTheOpenAPIClient()
{
    var factory = new TestApplication<Program>();
    var httpClient = factory.CreateClient();
    var generatedClient = new GeneratedClient("http://localhost/", httpClient);
    var fuzzer = new ApiFuzzerWithVerifier(generatedClient);
    await fuzzer.TestWholeApi();
}
```

---

### Example 6: Docker-based Fuzzing Test

From README - CI/CD example:

```csharp
var dockerImageUrl = "fusedrill/testapi:latest";
var containerName = "testapi";
var apiBaseUrl = "http://localhost:8080/";
var openApiSwaggerUrl = "http://localhost:8080/swagger/v1/swagger.json";

var containerBuilder = new ContainerBuilder()
    .WithImage(dockerImageUrl)
    .WithName(containerName)
    .WithPortBinding(8080, 8080)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

var container = containerBuilder.Build();
await container.StartAsync();

try
{
    await Task.Delay(1000);
    using var httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    var fuzzer = new ApiFuzzerWithVerifier(httpClient, openApiSwaggerUrl);
    await fuzzer.TestWholeApi();
}
finally
{
    await container.StopAsync();
    await container.DisposeAsync();
}
```


### Example 7: Single File with Shebang - Minimal One-Liner

Quick one-liner using file-based app with shebang for ad-hoc testing.
Always save your quick fuzzing scripts for future use in FuzLibrary folder, for future intent analysis.

```csharp
#!/usr/bin/env dotnet

// Include NuGet packages inline (for packages from nuget.org)
#:package FuseDrill.Core@*-*
#:package FuseDrill@*-*

using System;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using FuseDrill.Core;

// Create HttpClient and configure base address
var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5184/")
};

// Create fuzzer with API endpoint
var apiFuzzer = new ApiFuzzer(
    httpClient, 
"http://localhost:5184/swagger/v1/swagger.json"
);

// Run fuzzing and get results
var results = await apiFuzzer.TestWholeApi();

// Output using properly configured JsonSerializerOptions (required for .NET 10+)
var jsonOptions = new System.Text.Json.JsonSerializerOptions
{
    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    TypeInfoResolver = System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver.Default
};

var jsonString = System.Text.Json.JsonSerializer.Serialize(results, jsonOptions);
Console.WriteLine(jsonString);
```

**IMPORTANT**: In .NET 10+, JSON reflection is disabled by default. You MUST either:

1. **Use FuseDrill's SerializerOptions** (if available in your version):
   ```csharp
   var jsonString = System.Text.Json.JsonSerializer.Serialize(
       results, 
       FuseDrill.Core.SerializerOptions.GetOptions()
   );
   ```

2. **OR configure TypeInfoResolver explicitly** (as shown above):
   ```csharp
   var jsonOptions = new System.Text.Json.JsonSerializerOptions
   {
       TypeInfoResolver = System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver.Default
   };
   ```

3. **OR enable in .csproj** (not recommended for production):
   ```xml
   <JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
   ```

**Run:**
```bash
dotnet Example7.cs   # Windows
./Example7.cs       # Unix (after chmod +x Example7.cs)
```
```

---

## Docker CLI Usage

For quick testing without writing code:

```bash
# Basic usage
docker run --network host --rm \
  -e FUSEDRILL_BASE_ADDRESS="https://api.example.com" \
  -e FUSEDRILL_OPENAPI_URL="https://api.example.com/openapi.yaml" \
  ghcr.io/fusedrill/fusedrill-cli:latest

# With authentication
docker run --network host --rm \
  -e FUSEDRILL_BASE_ADDRESS="https://api.example.com" \
  -e FUSEDRILL_OPENAPI_URL="https://api.example.com/openapi.yaml" \
  -e FUSEDRILL_OAUTH_HEADER="Bearer your-token" \
  ghcr.io/fusedrill/fusedrill-cli:latest

# Smoke test only
docker run --network host --rm \
  -e FUSEDRILL_BASE_ADDRESS="https://api.example.com" \
  -e FUSEDRILL_OPENAPI_URL="https://api.example.com/openapi.yaml" \
  -e SMOKE_FLAG="true" \
  ghcr.io/fusedrill/fusedrill-cli:latest
```

---

## GitHub Actions Workflow

From README - CI/CD example:

```yml
name: FuseDrill Fuzzing/Simulation Testing

on:
  push:
    branches:
      - main
  pull_request:
    branches:
      - main

jobs:
  fuzz-test:
    runs-on: ubuntu-latest

    steps:
    - name: Pull FuseDrill test api Docker Image
      run: docker pull ghcr.io/fusedrill/fusedrill/testapi:latest
      
    - name: Run Test API
      run: |
        docker run -d \
          -e ASPNETCORE_ENVIRONMENT="Development" \
          -p 8080:8080 \
          ghcr.io/fusedrill/fusedrill/testapi:latest
        
    - name: Wait for Test API to be Ready
      run: |
        until curl -s http://localhost:8080/swagger/v1/swagger.json; do
          echo "Waiting for Test API to start..."
          sleep 5
        done

    - name: Pull FuseDrill Docker Image
      run: docker pull ghcr.io/fusedrill/fusedrill-cli:latest

    - name: Run FuseDrill CLI in Docker
      run: |
        docker run --network host --rm \
          -e FUSEDRILL_BASE_ADDRESS="http://localhost:8080/" \
          -e FUSEDRILL_OPENAPI_URL="http://localhost:8080/swagger/v1/swagger.json" \
          -e GITHUB_TOKEN="${{ secrets.GITHUB_TOKEN }}" \
          -e SMOKE_FLAG="true" \
          -e GITHUB_REPOSITORY_OWNER="${{ github.repository_owner }}" \
          -e GITHUB_REPOSITORY="${{ github.repository }}" \
          -e GITHUB_HEAD_REF="${{ github.head_ref }}" \
          -e GEMINI_API_KEY="${{ secrets.GEMINI_API_KEY }}" \
          ghcr.io/fusedrill/fusedrill-cli:latest
```

---

## Interpreting Results

### Successful Fuzzing
- All endpoints returned expected responses
- No breaking changes detected
- API is stable

### Failed Fuzzing
- Check the error messages:
  - **404 Not Found**: Endpoint path changed
  - **500 Internal Server Error**: Server bug
  - **401/403 Unauthorized**: Auth issue (not a real failure)
  - **Schema Mismatch**: Response format changed

### Next Steps After Getting Results

1. **Review failures** - Understand what broke and why
2. **Fix API** - Update code to match expected behavior
3. **Re-run fuzzing** - Verify the fix
4. **Commit snapshot** - Save the baseline for future comparisons
5. **Add to CI/CD** - Automate fuzzing on every push/PR

## 🔑 Critical: Always Ask "What's Next?"

**After EVERY fuzzing operation, you MUST explicitly ask the user what they want to do next.**

Do NOT proceed to the next step without user confirmation. Use this template:

```
=== RESULTS SUMMARY ===
- Endpoints tested: X
- Successful: Y
- Failed: Z (specify which)

What's Next?
1. Test specific endpoints
2. Add a fuzzing test to your codebase  
3. Run all except list
4. Generate test template
5. Something else - [Ask user]
```

The ONLY exceptions are:
- If the user explicitly says "do X next" - then do it
- If there are no endpoints to test - explain why and stop

---

## Common Scenarios

### Scenario 1: First Time Testing
```
User: "I want to test my API"
→ Ask for: Base URL, OpenAPI URL, optional auth
→ Start the API server (if local)
→ Run first pass fuzzing (see Tool 1 steps)
→ Present results in table format
→ **MUST ASK**: "Following the fuzzing test, what would you like to do now?"
  1. Test specific endpoints
  2. Add a fuzzing test to your codebase
  3. Run all except list
  4. Generate test template
  5. Something else
```

### Scenario 2: Testing After API Changes
```
User: "I made changes, does anything break?"
→ Run first pass fuzzing
→ Compare results to previous snapshot (if exists)
→ Report any breaking changes
→ **MUST ASK**: "What would you like to do with these results?"
```

### Scenario 3: Adding Tests to Project
```
User: "Add fuzzing tests to my project"
→ Ask: Single endpoint or all endpoints?
→ Use Tool 4: Generate C# Test Template
→ Provide the appropriate template
→ Explain integration steps
→ **MUST ASK**: "Would you like me to create this test file in your project?"
```

### Scenario 4: Exclude Destructive Endpoints
```
User: "Test all except DELETE endpoints"
→ Ask for: List of endpoints to exclude
→ Use Tool 2: Fuzz All Except List
→ Present results
→ **MUST ASK**: "Would you like to test the excluded endpoints separately?"
```

### Scenario 5: Test Specific Endpoint
```
User: "Test the /users endpoint specifically"
→ Ask for: Endpoint name and parameters
→ Use Tool 3: Specific Endpoint Fuzzing
→ Present detailed results
→ **MUST ASK**: "What would you like to do next?"
```

---

## Important Notes

- **ALWAYS ask "What's Next?"** after presenting results - never proceed without user confirmation
- FuseDrill generates random test values - results may vary between runs
- Use a fixed seed for reproducible tests
- Results can be serialized to JSON and committed as snapshots
- API must have a valid OpenAPI/Swagger spec
- For ASP.NET Core with top-level statements, add `public partial class Program { }`
- The fuzzer tests API contract, not business logic correctness
- Run with `--network host` to access localhost APIs from Docker