# Plan: Achieving 100% Code Coverage in API Fuzzing

## Current State Analysis

### What Exists
- `ApiFuzzer.cs` - Generates test suites from OpenAPI specs
- `DataGenerationHelper.cs` - Random data generation (dumb fuzzing)
- `Permutations.cs` - Simple permutation strategies (1, 2, or all)
- `Cover.cs` - **COMMENTED OUT** Coverlet integration for coverage tracking

### Current Limitations
1. **No coverage feedback** - The entire coverage tracking is commented out
2. **Pure random data** - Uses `Random()` with no intelligence
3. **Fixed seed** - Same test runs every time, same coverage gaps
4. **No edge case awareness** - Doesn't generate boundary values
5. **No mutation** - Doesn't evolve inputs based on feedback

---

## The Problem: Why Dumb Fuzzing Can't Reach 100%

```
Current approach: Random values → API → Check response
Problem: Random values miss:
  - Boundary conditions (min/max values)
  - Required combinations (if A then B must be present)
  - Type coercion edge cases (string "123" vs int 123)
  - Schema constraints (regex patterns, length limits)
  - Null/empty variations
  - Invalid but parseable data
```

---

## Multi-Layer Strategy for 100% Coverage

### Layer 1: Coverage-Guided Fuzzing (AFL-style)

**Concept**: Use coverage feedback to guide input generation

```csharp
// Pseudo-code
public class CoverageGuidedFuzzer
{
    private Dictionary<CoveragePoint, int> _coverageCounts;
    private Queue<InputSeed> _interestingInputs;
    
    public void Fuzz()
    {
        var initialInputs = GenerateSchemaBasedInputs();
        
        foreach (var input in initialInputs)
        {
            var coverage = ExecuteAndMeasureCoverage(input);
            
            if (coverage.IsInteresting(_coverageCounts))
            {
                _interestingInputs.Enqueue(input);
                _coverageCounts.Merge(coverage);
                
                // Generate mutations
                foreach (var mutation in Mutate(input))
                {
                    var mutatedCoverage = ExecuteAndMeasureCoverage(mutation);
                    if (mutatedCoverage.IsInteresting(_coverageCounts))
                    {
                        _interestingInputs.Enqueue(mutation);
                    }
                }
            }
        }
    }
}
```

**Implementation Requirements**:
1. Uncomment and fix `Cover.cs` with Coverlet integration
2. Track line/branch coverage per API call
3. Identify "uncovered" code paths
4. Generate inputs specifically targeting uncovered branches

---

### Layer 2: Schema-Aware Edge Case Generation

**Concept**: Parse OpenAPI schema and generate specific edge cases

```csharp
public class SchemaEdgeCaseGenerator
{
    public IEnumerable<object> GenerateEdgeCases(JsonSchemaProperty schema)
    {
        var edgeCases = new List<object>();
        
        // Boundary values for numeric types
        if (schema.Type == JsonObjectType.Integer || schema.Type == JsonObjectType.Number)
        {
            edgeCases.Add(schema.Minimum ?? 0);
            edgeCases.Add(schema.Maximum ?? int.MaxValue);
            edgeCases.Add(schema.Minimum - 1);  // Underflow
            edgeCases.Add(schema.Maximum + 1);  // Overflow
            edgeCases.Add(0);
            edgeCases.Add(-1);
        }
        
        // String constraints
        if (schema.Type == JsonObjectType.String)
        {
            edgeCases.Add("");  // Empty
            edgeCases.Add(null);  // Null if nullable
            
            if (schema.MinLength > 0)
                edgeCases.Add(new string('a', schema.MinLength - 1));  // Under min
            
            if (schema.MaxLength.HasValue)
                edgeCases.Add(new string('a', schema.MaxLength.Value + 1));  // Over max
            
            if (schema.Pattern != null)
                edgeCases.Add("!@#$%^&*");  // Invalid pattern
        }
        
        // Array constraints
        if (schema.Type == JsonObjectType.Array)
        {
            edgeCases.Add(new object[] { });  // Empty array
            edgeCases.Add(new[] { schema.ItemSchema.Default });  // Single item
            edgeCases.Add(Enumerable.Range(0, 100).Select(_ => Generate(schema.ItemSchema)).ToArray());  // Large array
        }
        
        // Enum values
        if (schema.EnumNames?.Any() == true)
        {
            edgeCases.AddRange(schema.EnumNames);
        }
        
        // Required combinations
        if (schema.Required?.Any() == true)
        {
            foreach (var reqProp in schema.Required)
            {
                var obj = new ExpandoObject() as IDictionary<string, object>;
                obj[reqProp] = Generate(schema.Properties[reqProp]);
                edgeCases.Add(obj);
            }
        }
        
        return edgeCases;
    }
}
```

---

### Layer 3: Combinatorial Testing (Pairwise)

**Concept**: Generate minimum set of combinations that cover all parameter interactions

```
Problem: For 10 parameters with 3 values each = 59,049 combinations
Solution: Pairwise testing = ~100 combinations (covers 90%+ bugs)
```

```csharp
public class CombinatorialGenerator
{
    public IEnumerable<Dictionary<string, object>> GeneratePairwise(
        List<Parameter> parameters,
        Dictionary<string, List<object>> parameterValues)
    {
        // Use existing libraries like NComb.Quality or implement IPO algorithm
        
        var strength = 2;  // Pairwise (2-way combinations)
        var combinations = GenerateCoveringArray(parameters, parameterValues, strength);
        
        return combinations;
    }
    
    private IEnumerable<Dictionary<string, object>> GenerateCoveringArray(
        List<Parameter> parameters,
        Dictionary<string, List<object>> values,
        int strength)
    {
        // Implementation of IPO (In-Parameter Order) algorithm
        // or use: NComb.Quality for production implementation
    }
}
```

---

### Layer 4: LLM-Guided Smart Fuzzing

**Concept**: Use AI to generate inputs that explore edge cases

```csharp
public class LlmGuidedFuzzer
{
    private ILlmClient _llm;
    
    public async Task<List<object>> GenerateSmartInputs(
        OpenApiOperation operation,
        List<object> attemptedInputs,
        CoverageReport coverage)
    {
        var uncoveredBranches = coverage.GetUncoveredBranches();
        
        var prompt = $"""
        Generate test inputs for this API endpoint:
        
        Operation: {operation.OperationId}
        Method: {operation.HttpMethod}
        Path: {operation.Path}
        
        Parameters:
        {string.Join("\n", operation.Parameters.Select(p => $"- {p.Name}: {p.Schema}"))}
        
        Already tried:
        {string.Join("\n", attemptedInputs.Select(i => $"- {Json.Serialize(i)}"))}
        
        Code branches not yet covered:
        {string.Join("\n", uncoveredBranches.Select(b => $"- {b}"))}
        
        Generate JSON array of input objects that would:
        1. Cover the uncovered branches
        2. Test boundary conditions
        3. Include edge cases like null, empty, max values
        """;
        
        var response = await _llm.complete(prompt);
        return ParseLlmResponse(response);
    }
}
```

---

## Implementation Roadmap

### Phase 1: Foundation (Week 1)
- [ ] Fix and uncomment `Cover.cs` with Coverlet integration
- [ ] Add coverage tracking per API call
- [ ] Create `CoverageReport` class
- [ ] Track line/branch coverage in `TestSuite`

### Phase 2: Schema-Aware Generation (Week 2)
- [ ] Create `SchemaEdgeCaseGenerator.cs`
- [ ] Parse OpenAPI schemas for constraints
- [ ] Generate boundary values (min, max, min-1, max+1)
- [ ] Generate constraint violations (overlength, invalid patterns)
- [ ] Handle required/optional combinations

### Phase 3: Combinatorial Testing (Week 3)
- [ ] Create `CombinatorialGenerator.cs`
- [ ] Implement pairwise (2-way) combination generation
- [ ] Integrate with `DataGenerationHelper`
- [ ] Add configuration for testing strength

### Phase 4: Coverage-Guided Loop (Week 4)
- [ ] Create `CoverageGuidedFuzzer.cs`
- [ ] Implement mutation strategies (bitflip, arith, dictionary)
- [ ] Track and prioritize inputs that discover new coverage
- [ ] Implement "interesting input" scoring

### Phase 5: LLM Integration (Week 5)
- [ ] Create `LlmGuidedFuzzer.cs`
- [ ] Integrate with existing LLM client
- [ ] Generate targeted inputs for uncovered branches
- [ ] Analyze error responses for hints

---

## File Changes Summary

### New Files
```
src/FuseDrill.Core/
├── Coverage/
│   ├── CoverageTracker.cs      # Track coverage per test
│   ├── CoverageReport.cs       # Coverage data structure
│   └── CoverletIntegration.cs  # Coverlet wrapper
├── Generation/
│   ├── SchemaEdgeCaseGenerator.cs    # Edge case from schema
│   ├── CombinatorialGenerator.cs      # Pairwise combinations
│   ├── MutationEngine.cs              # Input mutations
│   └── LlmGuidedGenerator.cs         # LLM-based generation
└── Fuzzing/
    ├── CoverageGuidedFuzzer.cs       # Main coverage-guided fuzzer
    └── FuzzingStrategy.cs            # Strategy pattern for different approaches
```

### Modified Files
```
src/FuseDrill.Core/
├── ApiFuzzer.cs           # Integrate new fuzzing strategies
├── DataGenerationHelper.cs # Add edge case generation
└── TestSuite.cs           # Add coverage tracking
```

---

## Expected Results

### Coverage Improvement
| Current | After Phase 2 | After Phase 4 | After Phase 5 |
|---------|---------------|---------------|---------------|
| ~40-60% | ~70-80% | ~90-95% | ~95-100% |

### Key Metrics
- **Coverage**: Line/branch coverage per API call
- **Efficiency**: Inputs per percentage point of coverage
- **Bugs Found**: Unique exceptions/crashes discovered
- **Time to Coverage**: Time to reach 80%/90%/100%

---

## Challenges & Mitigations

| Challenge | Mitigation |
|-----------|------------|
| Infinite loops in mutation | Max iterations, seed tracking |
| Duplicate coverage | Deduplication of inputs |
| Performance | Parallel execution, caching |
| Schema complexity | Library support (NJsonSchema) |
| LLM cost | Caching, batch requests |

---

## Success Criteria

- [ ] 100% line coverage on all API endpoints
- [ ] 100% branch coverage on all decision points  
- [ ] All schema constraints tested (min, max, pattern, required)
- [ ] All parameter combinations tested (pairwise)
- [ ] Coverage report generated per fuzzing run
- [ ] Deterministic runs with seed preservation

---

## Next Steps

1. **Start with Phase 1** - Fix coverage tracking first
2. **Add instrumentation** to measure current coverage
3. **Identify gaps** - What code is never reached?
4. **Implement targeted generators** for the gaps
5. **Iterate** until 100% coverage

---

## References

- [AFL - American Fuzzy Lop](https://github.com/google/AFL)
- [Coverlet Code Coverage](https://github.com/coverlet-coverage/coverlet)
- [Combinatorial Testing](https://www.pairwise.org/)
- [NComb.Quality](https://github.com/kspearrin/NComb.Quality) - C# combinatorial testing
- [NJsonSchema](https://github.com/RicoSuter/NJsonSchema) - Schema parsing
