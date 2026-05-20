using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FuseDrill.Core.Coverage;
using FuseDrill.Core.Generation;

namespace FuseDrill.Core.Fuzzing;

public interface ICoverageGuidedFuzzer
{
    Task<FuzzingResult> FuzzWithCoverageGuidedAsync(CoverageOptions options);
    CoverageReport? CurrentCoverage { get; }
    List<CoverageGap> RemainingGaps { get; }
}

public class FuzzingResult
{
    public bool Success { get; set; }
    public int TotalIterations { get; set; }
    public int UniqueInputsGenerated { get; set; }
    public double FinalCoverage { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public CoverageReport? FinalCoverageReport { get; set; }
    public List<string> DiscoveredExceptions { get; set; } = new();
    public List<CoverageGap> UncoveredGaps { get; set; } = new();
    public Dictionary<string, object> Statistics { get; set; } = new();
}

public class CoverageOptions
{
    public int MaxIterations { get; set; } = 1000;
    public int MaxDurationSeconds { get; set; } = 300;
    public int PopulationSize { get; set; } = 100;
    public double TargetCoverage { get; set; } = 100.0;
    public bool UseSchemaGeneration { get; set; } = true;
    public bool UseCombinatorialTesting { get; set; } = true;
    public bool UseMutation { get; set; } = true;
    public int MutationCount { get; set; } = 10;
    public Action<string>? Logger { get; set; }
}

public class CoverageGuidedFuzzer : ICoverageGuidedFuzzer
{
    private readonly IApiFuzzer _apiFuzzer;
    private readonly ICoverageTracker _coverageTracker;
    private readonly SchemaEdgeCaseGenerator _edgeCaseGenerator;
    private readonly ICombinatorialGenerator _combinatorialGenerator;
    private readonly MutationEngine _mutationEngine;
    
    public CoverageReport? CurrentCoverage { get; private set; }
    public List<CoverageGap> RemainingGaps { get; private set; } = new();
    
    private readonly List<InputSeed> _population = new();
    private readonly HashSet<string> _seenInputs = new();
    private int _iteration = 0;
    private DateTime _startTime;
    private readonly List<string> _discoveredExceptions = new();
    
    public CoverageGuidedFuzzer(
        IApiFuzzer apiFuzzer,
        ICoverageTracker coverageTracker,
        SchemaEdgeCaseGenerator edgeCaseGenerator,
        ICombinatorialGenerator combinatorialGenerator)
    {
        _apiFuzzer = apiFuzzer;
        _coverageTracker = coverageTracker;
        _edgeCaseGenerator = edgeCaseGenerator;
        _combinatorialGenerator = combinatorialGenerator;
        _mutationEngine = new MutationEngine();
    }
    
    public async Task<FuzzingResult> FuzzWithCoverageGuidedAsync(CoverageOptions options)
    {
        _startTime = DateTime.UtcNow;
        _population.Clear();
        _seenInputs.Clear();
        _iteration = 0;
        _discoveredExceptions.Clear();
        
        var result = new FuzzingResult
        {
            Success = false,
            TotalDuration = TimeSpan.Zero
        };
        
        try
        {
            await GenerateInitialPopulation(options);
            
            var sw = Stopwatch.StartNew();
            
            while (_iteration < options.MaxIterations && 
                   sw.Elapsed.TotalSeconds < options.MaxDurationSeconds &&
                   CurrentCoverage?.LineCoveragePercentage < options.TargetCoverage)
            {
                _iteration++;
                
                await EvolveAndExecute(options);
                
                CurrentCoverage = _coverageTracker.CaptureCoverage();
                
                if (_iteration % 10 == 0)
                {
                    options.Logger?.Invoke($"Iteration {_iteration}: Coverage = {CurrentCoverage.LineCoveragePercentage:F2}%");
                }
                
                if (CurrentCoverage.LineCoveragePercentage >= options.TargetCoverage)
                {
                    result.Success = true;
                    break;
                }
            }
            
            sw.Stop();
            result.TotalIterations = _iteration;
            result.FinalCoverage = CurrentCoverage?.LineCoveragePercentage ?? 0;
            result.TotalDuration = sw.Elapsed;
            result.FinalCoverageReport = CurrentCoverage;
            result.DiscoveredExceptions = _discoveredExceptions;
            result.UncoveredGaps = RemainingGaps;
            result.Statistics = new Dictionary<string, object>
            {
                ["PopulationSize"] = _population.Count,
                ["UniqueInputs"] = _seenInputs.Count,
                ["MutationsPerformed"] = _iteration * options.MutationCount,
                ["CoveragePerIteration"] = CurrentCoverage?.LineCoveragePercentage ?? 0
            };
        }
        catch (Exception ex)
        {
            result.DiscoveredExceptions.Add(ex.Message);
            options.Logger?.Invoke($"Error during fuzzing: {ex.Message}");
        }
        
        return result;
    }
    
    private async Task GenerateInitialPopulation(CoverageOptions options)
    {
        options.Logger?.Invoke("Generating initial population...");
        
        if (options.UseSchemaGeneration)
        {
            await GenerateSchemaBasedInputs();
        }
        
        if (options.UseCombinatorialTesting)
        {
            GenerateCombinatorialInputs();
        }
        
        if (!_population.Any())
        {
            var seed = new InputSeed
            {
                Values = new Dictionary<string, object>(),
                FitnessScore = 1,
                IsInteresting = true
            };
            _population.Add(seed);
        }
        
        options.Logger?.Invoke($"Generated {_population.Count} initial inputs");
    }
    
    private async Task GenerateSchemaBasedInputs()
    {
        try
        {
            var edgeCases = new List<object>();
            
            var seed = new InputSeed
            {
                Values = new Dictionary<string, object>(),
                FitnessScore = 10,
                IsInteresting = true
            };
            
            _population.Add(seed);
            _seenInputs.Add(SerializeInput(seed.Values));
        }
        catch (Exception ex)
        {
            _discoveredExceptions.Add($"Schema generation error: {ex.Message}");
        }
    }
    
    private void GenerateCombinatorialInputs()
    {
        try
        {
            var seed = new InputSeed
            {
                Values = new Dictionary<string, object>(),
                FitnessScore = 5,
                IsInteresting = true
            };
            
            if (!_seenInputs.Contains(SerializeInput(seed.Values)))
            {
                _population.Add(seed);
                _seenInputs.Add(SerializeInput(seed.Values));
            }
        }
        catch (Exception ex)
        {
            _discoveredExceptions.Add($"Combinatorial generation error: {ex.Message}");
        }
    }
    
    private async Task EvolveAndExecute(CoverageOptions options)
    {
        var currentCoverage = _coverageTracker.CaptureCoverage();
        var gaps = currentCoverage?.UncoveredLines ?? new List<CoverageGap>();
        
        foreach (var seed in _population.Take(options.PopulationSize))
        {
            if (options.UseMutation)
            {
                var mutations = _mutationEngine.Mutate(seed, options.MutationCount);
                
                foreach (var mutant in mutations)
                {
                    var inputKey = SerializeInput(mutant.Values);
                    
                    if (!_seenInputs.Contains(inputKey))
                    {
                        _seenInputs.Add(inputKey);
                        mutant.IsInteresting = await ExecuteAndCheckCoverage(mutant, gaps);
                        mutant.FitnessScore = mutant.IsInteresting ? 100 : 1;
                        _population.Add(mutant);
                    }
                }
            }
            
            if (options.UseSchemaGeneration && _iteration % 5 == 0)
            {
                var smartMutant = _mutationEngine.SmartMutate(seed, gaps.Take(10).ToList());
                var smartKey = SerializeInput(smartMutant.Values);
                
                if (!_seenInputs.Contains(smartKey))
                {
                    _seenInputs.Add(smartKey);
                    smartMutant.IsInteresting = await ExecuteAndCheckCoverage(smartMutant, gaps);
                    _population.Add(smartMutant);
                }
            }
        }
        
        PrunePopulation(options.PopulationSize);
        
        RemainingGaps = gaps;
    }
    
    private async Task<bool> ExecuteAndCheckCoverage(InputSeed input, List<CoverageGap> targetGaps)
    {
        try
        {
            _coverageTracker.Reset();
            
            var baselineCoverage = _coverageTracker.CaptureCoverage();
            
            await ExecuteInput(input);
            
            var newCoverage = _coverageTracker.CaptureCoverage();
            
            var newLines = newCoverage.CoveredLines - baselineCoverage.CoveredLines;
            var newBranches = newCoverage.CoveredBranches - baselineCoverage.CoveredBranches;
            
            return newLines > 0 || newBranches > 0;
        }
        catch (Exception ex)
        {
            if (!_discoveredExceptions.Contains(ex.Message))
            {
                _discoveredExceptions.Add(ex.Message);
            }
            return false;
        }
    }
    
    private async Task ExecuteInput(InputSeed input)
    {
        try
        {
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _discoveredExceptions.Add($"Input execution error: {ex.Message}");
        }
    }
    
    private void PrunePopulation(int maxSize)
    {
        if (_population.Count <= maxSize) return;
        
        _population.Sort((a, b) => b.FitnessScore.CompareTo(a.FitnessScore));
        
        var keepCount = Math.Max(maxSize / 2, _population.Count / 2);
        
        var toKeep = _population.Take(keepCount).ToList();
        var toAdd = _population.Skip(keepCount)
            .OrderByDescending(_ => Guid.NewGuid())
            .Take(maxSize - keepCount)
            .ToList();
        
        _population.Clear();
        _population.AddRange(toKeep);
        _population.AddRange(toAdd);
    }
    
    private string SerializeInput(Dictionary<string, object> values)
    {
        return string.Join("|", values.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
    }
}

public class FuzzingProgressReporter
{
    private readonly ICoverageGuidedFuzzer _fuzzer;
    private readonly System.Timers.Timer _timer;
    private int _reportIntervalSeconds;
    
    public event Action<string>? OnProgress;
    public event Action<FuzzingResult>? OnComplete;
    
    public FuzzingProgressReporter(ICoverageGuidedFuzzer fuzzer, int reportIntervalSeconds = 10)
    {
        _fuzzer = fuzzer;
        _reportIntervalSeconds = reportIntervalSeconds;
        _timer = new System.Timers.Timer(reportIntervalSeconds * 1000);
        _timer.Elapsed += (s, e) => ReportProgress();
    }
    
    public void Start()
    {
        _timer.Start();
    }
    
    public void Stop()
    {
        _timer.Stop();
    }
    
    private void ReportProgress()
    {
        var coverage = _fuzzer.CurrentCoverage;
        var gaps = _fuzzer.RemainingGaps;
        
        var report = $"""
            Progress Report
            ==============
            Current Coverage: {coverage?.LineCoveragePercentage ?? 0:F2}%
            Lines Covered: {coverage?.CoveredLines ?? 0}
            Branches Covered: {coverage?.CoveredBranches ?? 0}
            Remaining Gaps: {gaps.Count}
            """;
        
        OnProgress?.Invoke(report);
    }
    
    public async Task RunWithReportingAsync(CoverageOptions options, CancellationToken cancellation)
    {
        Start();
        
        try
        {
            var result = await _fuzzer.FuzzWithCoverageGuidedAsync(options);
            
            Stop();
            
            ReportProgress();
            OnComplete?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            Stop();
            OnProgress?.Invoke("Fuzzing cancelled by user");
        }
    }
}
