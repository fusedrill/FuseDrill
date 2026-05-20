using System.Collections.Concurrent;
using System.Diagnostics;

namespace FuseDrill.Core.Generation;

public interface ICombinatorialGenerator
{
    IEnumerable<Dictionary<string, object>> GeneratePairwise(
        List<Parameter> parameters,
        Dictionary<string, List<object>> parameterValues);
    
    IEnumerable<Dictionary<string, object>> GenerateTwise(
        List<Parameter> parameters,
        Dictionary<string, List<object>> parameterValues,
        int strength);
}

public class CombinatorialGenerator : ICombinatorialGenerator
{
    private readonly Random _random;
    private readonly int _maxIterations;
    
    public CombinatorialGenerator(int seed = 1234567, int maxIterations = 10000)
    {
        _random = new Random(seed);
        _maxIterations = maxIterations;
    }
    
    public IEnumerable<Dictionary<string, object>> GeneratePairwise(
        List<Parameter> parameters,
        Dictionary<string, List<object>> parameterValues)
    {
        return GenerateTwise(parameters, parameterValues, 2);
    }
    
    public IEnumerable<Dictionary<string, object>> GenerateTwise(
        List<Parameter> parameters,
        Dictionary<string, List<object>> parameterValues,
        int strength = 2)
    {
        if (parameters.Count == 0)
        {
            yield break;
        }
        
        if (parameters.Count == 1)
        {
            var param = parameters[0];
            foreach (var value in parameterValues[param.Name])
            {
                yield return new Dictionary<string, object> { { param.Name, value } };
            }
            yield break;
        }
        
        var combinations = new List<Dictionary<string, object>>();
        var coveredPairs = new HashSet<string>();
        var iterations = 0;
        
        var paramNames = parameters.Select(p => p.Name).ToList();
        var paramValues = new Dictionary<string, List<object>>(parameterValues);
        
        while (iterations < _maxIterations)
        {
            iterations++;
            
            var combination = new Dictionary<string, object>();
            
            foreach (var paramName in paramNames)
            {
                var values = paramValues[paramName];
                combination[paramName] = values[_random.Next(values.Count)];
            }
            
            var isNew = false;
            
            if (strength == 2)
            {
                for (int i = 0; i < paramNames.Count; i++)
                {
                    for (int j = i + 1; j < paramNames.Count; j++)
                    {
                        var pairKey = $"{paramNames[i]}={combination[paramNames[i]]}|{paramNames[j]}={combination[paramNames[j]]}";
                        if (!coveredPairs.Contains(pairKey))
                        {
                            coveredPairs.Add(pairKey);
                            isNew = true;
                        }
                    }
                }
            }
            else if (strength == 3)
            {
                for (int i = 0; i < paramNames.Count; i++)
                {
                    for (int j = i + 1; j < paramNames.Count; j++)
                    {
                        for (int k = j + 1; k < paramNames.Count; k++)
                        {
                            var tripleKey = $"{paramNames[i]}={combination[paramNames[i]]}|{paramNames[j]}={combination[paramNames[j]]}|{paramNames[k]}={combination[paramNames[k]]}";
                            if (!coveredPairs.Contains(tripleKey))
                            {
                                coveredPairs.Add(tripleKey);
                                isNew = true;
                            }
                        }
                    }
                }
            }
            
            if (isNew)
            {
                combinations.Add(new Dictionary<string, object>(combination));
                
                if (strength == 2 && coveredPairs.Count >= GetExpectedPairsCount(paramNames.Count))
                {
                    break;
                }
                else if (strength == 3 && coveredPairs.Count >= GetExpectedTriplesCount(paramNames.Count))
                {
                    break;
                }
            }
            
            if (iterations >= _maxIterations)
            {
                Debug.WriteLine($"Reached max iterations: {_maxIterations}");
                break;
            }
        }
        
        foreach (var combo in combinations)
        {
            yield return combo;
        }
    }
    
    private int GetExpectedPairsCount(int paramCount)
    {
        if (paramCount < 2) return 0;
        return (paramCount * (paramCount - 1)) / 2;
    }
    
    private int GetExpectedTriplesCount(int paramCount)
    {
        if (paramCount < 3) return 0;
        return (paramCount * (paramCount - 1) * (paramCount - 2)) / 6;
    }
}

public class InputSeed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Dictionary<string, object> Values { get; set; } = new();
    public int FitnessScore { get; set; }
    public int TimesUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Coverage.CoverageReport? LastCoverage { get; set; }
    public List<string> DiscoveredBranches { get; set; } = new();
    public bool IsInteresting { get; set; }
    
    public InputSeed Clone()
    {
        return new InputSeed
        {
            Id = Guid.NewGuid(),
            Values = new Dictionary<string, object>(Values),
            FitnessScore = FitnessScore,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public class MutationEngine
{
    private readonly Random _random;
    private readonly List<IMutationStrategy> _strategies;
    
    public MutationEngine(int seed = 1234567)
    {
        _random = new Random(seed);
        _strategies = new List<IMutationStrategy>
        {
            new BitFlipMutation(_random),
            new ArithMutation(_random),
            new BoundaryMutation(_random),
            new NullMutation(_random),
            new SwapMutation(_random),
            new EmptyValueMutation(_random)
        };
    }
    
    public IEnumerable<InputSeed> Mutate(InputSeed seed, int count = 10)
    {
        for (int i = 0; i < count; i++)
        {
            var strategy = _strategies[_random.Next(_strategies.Count)];
            var mutated = strategy.Mutate(seed);
            yield return mutated;
        }
    }
    
    public InputSeed SmartMutate(InputSeed seed, List<Coverage.CoverageGap> targetGaps)
    {
        var mutated = seed.Clone();
        
        foreach (var gap in targetGaps.Take(5))
        {
            var propertyName = ExtractPropertyName(gap);
            if (mutated.Values.ContainsKey(propertyName))
            {
                var strategy = SelectStrategyForGap(gap);
                mutated.Values[propertyName] = strategy.GetMutatedValue(mutated.Values[propertyName], gap);
                mutated.IsInteresting = true;
            }
        }
        
        return mutated;
    }
    
    private string ExtractPropertyName(Coverage.CoverageGap gap)
    {
        var match = System.Text.RegularExpressions.Regex.Match(gap.MethodName, @"(\w+)_http_");
        return match.Success ? match.Groups[1].Value : gap.MethodName;
    }
    
    private IMutationStrategy SelectStrategyForGap(Coverage.CoverageGap gap)
    {
        if (gap.Reason.Contains("null"))
        {
            return _strategies.OfType<NullMutation>().First();
        }
        if (gap.Reason.Contains("boundary") || gap.Reason.Contains("range"))
        {
            return _strategies.OfType<BoundaryMutation>().First();
        }
        return _strategies[_random.Next(_strategies.Count)];
    }
}

public interface IMutationStrategy
{
    InputSeed Mutate(InputSeed seed);
    object GetMutatedValue(object original, Coverage.CoverageGap gap);
    string Name { get; }
}

public class BitFlipMutation : IMutationStrategy
{
    private readonly Random _random;
    
    public BitFlipMutation(Random random)
    {
        _random = random;
    }
    
    public string Name => "BitFlip";
    
    public InputSeed Mutate(InputSeed seed)
    {
        var mutated = seed.Clone();
        var keys = mutated.Values.Keys.ToList();
        if (keys.Count == 0) return mutated;
        
        var key = keys[_random.Next(keys.Count)];
        var value = mutated.Values[key];
        
        mutated.Values[key] = FlipBits(value);
        mutated.IsInteresting = true;
        return mutated;
    }
    
    public object GetMutatedValue(object original, Coverage.CoverageGap gap)
    {
        return FlipBits(original);
    }
    
    private object FlipBits(object value)
    {
        return value switch
        {
            int i => ~i,
            long l => ~l,
            bool b => !b,
            string s => string.Concat(s.Select(c => (char)(c ^ 0xFF))),
            byte[] b => b.Select(b => (byte)(b ^ 0xFF)).ToArray(),
            _ => value
        };
    }
}

public class ArithMutation : IMutationStrategy
{
    private readonly Random _random;
    private readonly int[] _operations = { 0, 1, -1, 2, -2, 10, -10, 100, -100 };
    
    public ArithMutation(Random random)
    {
        _random = random;
    }
    
    public string Name => "Arith";
    
    public InputSeed Mutate(InputSeed seed)
    {
        var mutated = seed.Clone();
        var keys = mutated.Values.Keys.ToList();
        if (keys.Count == 0) return mutated;
        
        var key = keys[_random.Next(keys.Count)];
        var value = mutated.Values[key];
        
        mutated.Values[key] = ApplyArith(value);
        return mutated;
    }
    
    public object GetMutatedValue(object original, Coverage.CoverageGap gap)
    {
        return ApplyArith(original);
    }
    
    private object ApplyArith(object value)
    {
        var op = _operations[_random.Next(_operations.Length)];
        
        return value switch
        {
            int i => i + op,
            long l => l + op,
            double d => d + op,
            float f => f + op,
            decimal d => d + op,
            _ => value
        };
    }
}

public class BoundaryMutation : IMutationStrategy
{
    private readonly Random _random;
    
    public BoundaryMutation(Random random)
    {
        _random = random;
    }
    
    public string Name => "Boundary";
    
    public InputSeed Mutate(InputSeed seed)
    {
        var mutated = seed.Clone();
        var keys = mutated.Values.Keys.ToList();
        if (keys.Count == 0) return mutated;
        
        var key = keys[_random.Next(keys.Count)];
        var value = mutated.Values[key];
        
        mutated.Values[key] = GetBoundaryValue(value);
        return mutated;
    }
    
    public object GetMutatedValue(object original, Coverage.CoverageGap gap)
    {
        return GetBoundaryValue(original);
    }
    
    private object GetBoundaryValue(object value)
    {
        return value switch
        {
            int i => new[] { int.MinValue, int.MaxValue, 0, 1, -1, i - 1, i + 1 }
                .OrderBy(_ => _random.Next()).First(),
            long l => new[] { long.MinValue, long.MaxValue, 0L, 1L, -1L }
                .OrderBy(_ => _random.Next()).First(),
            double d => new[] { double.Epsilon, double.MaxValue, double.MinValue, 0.0, 1.0, -1.0 }
                .OrderBy(_ => _random.Next()).First(),
            string s => new[] { "", "a", new string('a', 1000), new string('a', 10000) }
                .OrderBy(_ => _random.Next()).First(),
            _ => value
        };
    }
}

public class NullMutation : IMutationStrategy
{
    private readonly Random _random;
    
    public NullMutation(Random random)
    {
        _random = random;
    }
    
    public string Name => "Null";
    
    public InputSeed Mutate(InputSeed seed)
    {
        var mutated = seed.Clone();
        var keys = mutated.Values.Keys.ToList();
        if (keys.Count == 0) return mutated;
        
        var key = keys[_random.Next(keys.Count)];
        mutated.Values[key] = null!;
        mutated.IsInteresting = true;
        return mutated;
    }
    
    public object GetMutatedValue(object original, Coverage.CoverageGap gap)
    {
        return null!;
    }
}

public class SwapMutation : IMutationStrategy
{
    private readonly Random _random;
    
    public SwapMutation(Random random)
    {
        _random = random;
    }
    
    public string Name => "Swap";
    
    public InputSeed Mutate(InputSeed seed)
    {
        var mutated = seed.Clone();
        var keys = mutated.Values.Keys.ToList();
        if (keys.Count < 2) return mutated;
        
        var idx1 = _random.Next(keys.Count);
        var idx2 = _random.Next(keys.Count);
        while (idx2 == idx1) idx2 = _random.Next(keys.Count);
        
        var temp = mutated.Values[keys[idx1]];
        mutated.Values[keys[idx1]] = mutated.Values[keys[idx2]];
        mutated.Values[keys[idx2]] = temp;
        
        return mutated;
    }
    
    public object GetMutatedValue(object original, Coverage.CoverageGap gap)
    {
        return original;
    }
}

public class EmptyValueMutation : IMutationStrategy
{
    private readonly Random _random;
    
    public EmptyValueMutation(Random random)
    {
        _random = random;
    }
    
    public string Name => "Empty";
    
    public InputSeed Mutate(InputSeed seed)
    {
        var mutated = seed.Clone();
        var keys = mutated.Values.Keys.ToList();
        if (keys.Count == 0) return mutated;
        
        var key = keys[_random.Next(keys.Count)];
        var value = mutated.Values[key];
        
        mutated.Values[key] = GetEmptyValue(value);
        return mutated;
    }
    
    public object GetMutatedValue(object original, Coverage.CoverageGap gap)
    {
        return GetEmptyValue(original);
    }
    
    private object GetEmptyValue(object value)
    {
        if (value is string) return "";
        
        if (value is IEnumerable<object>)
        {
            return Array.Empty<object>();
        }
        
        if (value is IDictionary<object, object>)
        {
            return Activator.CreateInstance(value.GetType());
        }
        
        if (value.GetType().IsValueType)
        {
            return Activator.CreateInstance(value.GetType());
        }
        
        return "";
    }
}
