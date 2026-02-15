using System;
using System.Collections.Generic;
using System.Linq;

namespace FuseDrill.Core.Coverage;

public class CoverageReport
{
    public string AssemblyName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public int TotalLines { get; set; }
    public int CoveredLines { get; set; }
    public double LineCoveragePercentage => TotalLines > 0 
        ? Math.Round((double)CoveredLines / TotalLines * 100, 2) 
        : 0;
    
    public int TotalBranches { get; set; }
    public int CoveredBranches { get; set; }
    public double BranchCoveragePercentage => TotalBranches > 0 
        ? Math.Round((double)CoveredBranches / TotalBranches * 100, 2) 
        : 0;
    
    public int TotalMethods { get; set; }
    public int CoveredMethods { get; set; }
    public double MethodCoveragePercentage => TotalMethods > 0 
        ? Math.Round((double)CoveredMethods / TotalMethods * 100, 2) 
        : 0;
    
    public Dictionary<string, FileCoverage> Files { get; set; } = new();
    public Dictionary<string, int> HitCounts { get; set; } = new();
    public List<CoverageGap> UncoveredLines { get; set; } = new();
    public List<BranchCoverage> BranchCoverage { get; set; } = new();

    public int UniqueCoveragePoints => HitCounts.Count;

    public double TotalCoveragePercentage
    {
        get
        {
            var total = TotalLines + TotalBranches;
            var covered = CoveredLines + CoveredBranches;
            return total > 0 ? Math.Round((double)covered / total * 100, 2) : 0;
        }
    }

    public FuzzingSessionMetrics FuzzingMetrics { get; set; } = new();

    public CoverageSummary GetSummary()
    {
        return new CoverageSummary
        {
            LineCoverage = LineCoveragePercentage,
            BranchCoverage = BranchCoveragePercentage,
            MethodCoverage = MethodCoveragePercentage,
            TotalLines = TotalLines,
            CoveredLines = CoveredLines,
            TotalBranches = TotalBranches,
            CoveredBranches = CoveredBranches,
            Timestamp = Timestamp
        };
    }
}

public class FuzzingSessionMetrics
{
    public int TotalApiCalls { get; set; }
    public int UniqueInputsGenerated { get; set; }
    public int EdgeCasesGenerated { get; set; }
    public int CombinationsGenerated { get; set; }
    public int MutationsApplied { get; set; }
    public int IterationsCompleted { get; set; }
    public int ExceptionsEncountered { get; set; }
    public int UniqueExceptions { get; set; }
    public int InputsBeforeMinimization { get; set; }
    public int InputsAfterMinimization { get; set; }
    public double MinimizationReductionPercent { get; set; }
    public int UniqueBehaviors { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public List<string> DiscoveredExceptionTypes { get; set; } = new();
    public Dictionary<string, int> ResponseStatusCodes { get; set; } = new();
    public List<ApiCallMetrics> SlowestApiCalls { get; set; } = new();
}

public class ApiCallMetrics
{
    public string MethodName { get; set; } = string.Empty;
    public double ResponseTimeMs { get; set; }
    public int StatusCode { get; set; }
    public bool HadException { get; set; }
    public string? ExceptionType { get; set; }
}

public class CoverageSummary
{
    public double LineCoverage { get; set; }
    public double BranchCoverage { get; set; }
    public double MethodCoverage { get; set; }
    public int TotalLines { get; set; }
    public int CoveredLines { get; set; }
    public int TotalBranches { get; set; }
    public int CoveredBranches { get; set; }
    public DateTime Timestamp { get; set; }
}

public class FileCoverage
{
    public string FilePath { get; set; } = string.Empty;
    public int TotalLines { get; set; }
    public int CoveredLines { get; set; }
    public double Percentage => TotalLines > 0 
        ? Math.Round((double)CoveredLines / TotalLines * 100, 2) 
        : 0;
    
    public Dictionary<int, int> LineHits { get; set; } = new();
    public List<int> UncoveredLineNumbers { get; set; } = new();
}

public class CoverageGap
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class BranchCoverage
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string BranchType { get; set; } = string.Empty;
    public bool IsCovered { get; set; }
    public int HitCount { get; set; }
    public string Condition { get; set; } = string.Empty;
}

public class ApiCallCoverage
{
    public string MethodName { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public CoverageReport Coverage { get; set; } = new();
    public int InputIndex { get; set; }
    public object? Input { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool HasException { get; set; }
    public string? ExceptionType { get; set; }
    public List<CoverageGap> NewCoverageGaps { get; set; } = new();
}

public class FuzzingSessionCoverage
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    
    public int TotalApiCalls { get; set; }
    public int UniqueInputs { get; set; }
    public int Iterations { get; set; }
    
    public CoverageSummary InitialCoverage { get; set; } = new();
    public CoverageSummary FinalCoverage { get; set; } = new();
    public CoverageSummary BestCoverage { get; set; } = new();
    
    public List<ApiCallCoverage> ApiCallCoverages { get; set; } = new();
    public List<CoverageGap> AllUncoveredGaps { get; set; } = new();
    public List<string> DiscoveredExceptions { get; set; } = new();
    public Dictionary<string, int> CoverageProgress { get; set; } = new();
    
    public void AddIteration(CoverageSummary coverage, int iteration)
    {
        CoverageProgress[$"iteration_{iteration}"] = (int)coverage.LineCoverage;
        
        if (coverage.LineCoverage > BestCoverage.LineCoverage)
        {
            BestCoverage = coverage;
        }
    }
}
