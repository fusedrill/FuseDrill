using System;
using System.Threading.Tasks;
using FuseDrill.Core.Coverage;

namespace FuseDrill.Core;

public interface IApiFuzzer
{
    Task<FuzzerTests> TestWholeApi(Func<ApiCall, bool>? filter = null);
    Task<FuzzerTests> TestWholeApiWithCoverageGuidance(FuzzingOptions? options = null);
    CoverageReport? GetCoverageReport();
    void ResetCoverage();
}

public class FuzzingOptions
{
    public bool EnableCoverageGuidance { get; set; } = true;
    public bool EnableEdgeCaseGeneration { get; set; } = true;
    public bool EnableCombinatorialTesting { get; set; } = true;
    public bool EnableMutation { get; set; } = true;
    public bool MinimizeInputs { get; set; } = true;
    public int MaxIterations { get; set; } = 1000;
    public int MaxDurationSeconds { get; set; } = 300;
    public double TargetCoverage { get; set; } = 100.0;
    public int MutationCount { get; set; } = 10;
    public Action<string>? Logger { get; set; }
    public Func<ApiCall, bool>? Filter { get; set; }
}

public class CorpusMinimizer
{
    private readonly Dictionary<string, HashSet<string>> _responseSignatures = new();
    private readonly List<ApiCall> _minimalCorpus = new();
    private readonly HashSet<string> _seenSignatures = new();

    public List<ApiCall> Minimize(List<ApiCall> apiCalls, Action<string>? logger = null)
    {
        _minimalCorpus.Clear();
        _responseSignatures.Clear();
        _seenSignatures.Clear();

        logger?.Invoke($"Minimizing {apiCalls.Count} inputs...");

        foreach (var apiCall in apiCalls)
        {
            var signature = GetResponseSignature(apiCall);
            
            if (!_seenSignatures.Contains(signature))
            {
                _seenSignatures.Add(signature);
                _minimalCorpus.Add(CloneApiCall(apiCall));
                logger?.Invoke($"  Kept: {apiCall.MethodName} (new behavior)");
            }
            else
            {
                logger?.Invoke($"  Skipped: {apiCall.MethodName} (duplicate behavior)");
            }
        }

        var reduction = ((double)(apiCalls.Count - _minimalCorpus.Count) / apiCalls.Count * 100);
        logger?.Invoke($"Minimization complete: {apiCalls.Count} -> {_minimalCorpus.Count} inputs ({reduction:F1}% reduction)");

        return _minimalCorpus;
    }

    private static string GetResponseSignature(ApiCall apiCall)
    {
        var responseStr = apiCall.Response?.ToString() ?? "null";
        var paramStr = string.Join("|", apiCall.RequestParameters.Select(p => $"{p.Name}={p.Value}"));
        var exceptionType = apiCall.Response?.GetType().Name ?? "None";
        
        return $"{exceptionType}:{responseStr.Length}:{paramStr.GetHashCode()}";
    }

    private static ApiCall CloneApiCall(ApiCall original)
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
            Response = original.Response
        };
    }
}
