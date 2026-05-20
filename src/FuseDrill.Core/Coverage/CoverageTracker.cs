using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace FuseDrill.Core.Coverage;

using FuseDrill.Core;

public class TrackedApiCall
{
    public ApiCall ApiCall { get; set; } = new()
    {
        MethodName = "",
        RequestParameters = new List<ParameterValue>(),
        Response = null!
    };
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public bool IsCompleted { get; set; }
    public object? Response { get; set; }
    public Exception? Exception { get; set; }
}

public interface ICoverageTracker
{
    CoverageReport CaptureCoverage();
    CoverageReport CaptureCoverageForAssembly(Assembly assembly);
    void Reset();
    void MarkLineHit(string filePath, int lineNumber);
    void MarkBranchHit(string filePath, int lineNumber, string branchType);
    bool IsInteresting(CoverageReport newCoverage, CoverageReport baseline);
    List<CoverageGap> GetNewGaps(CoverageReport baseline, CoverageReport current);
    TrackedApiCall StartTracking(ApiCall apiCall);
    void CompleteTracking(TrackedApiCall trackedCall, object? response);
    void CompleteTracking(TrackedApiCall trackedCall, Exception exception);
    CoverageReport GenerateReport();
}

public class CoverageTracker : ICoverageTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, int>> _lineHits = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _branchHits = new();
    private readonly HashSet<string> _coveredLines = new();
    private readonly HashSet<string> _coveredBranches = new();
    private readonly ConcurrentDictionary<string, TrackedApiCall> _trackedCalls = new();
    
    private CoverageReport? _baseline;
    
    public TrackedApiCall StartTracking(ApiCall apiCall)
    {
        var trackedCall = new TrackedApiCall
        {
            ApiCall = apiCall,
            StartTime = DateTime.UtcNow
        };
        _trackedCalls[Guid.NewGuid().ToString()] = trackedCall;
        return trackedCall;
    }
    
    public void CompleteTracking(TrackedApiCall trackedCall, object? response)
    {
        trackedCall.IsCompleted = true;
        trackedCall.Response = response;
        trackedCall.ApiCall.Response = response;
    }
    
    public void CompleteTracking(TrackedApiCall trackedCall, Exception exception)
    {
        trackedCall.IsCompleted = true;
        trackedCall.Exception = exception;
    }
    
    public CoverageReport GenerateReport()
    {
        var report = new CoverageReport
        {
            Timestamp = DateTime.UtcNow,
            CoveredLines = _coveredLines.Count,
            CoveredBranches = _coveredBranches.Count,
            HitCounts = _lineHits
                .SelectMany(kv => kv.Value.Select(line => new KeyValuePair<string, int>($"{kv.Key}:{line.Key}", line.Value)))
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        };
        
        return report;
    }
    
    public void MarkLineHit(string filePath, int lineNumber)
    {
        var key = $"{filePath}:{lineNumber}";
        _coveredLines.Add(key);
        
        var fileDict = _lineHits.GetOrAdd(filePath, _ => new ConcurrentDictionary<int, int>());
        fileDict.AddOrUpdate(lineNumber, 1, (_, count) => count + 1);
    }
    
    public void MarkBranchHit(string filePath, int lineNumber, string branchType)
    {
        var key = $"{filePath}:{lineNumber}:{branchType}";
        _coveredBranches.Add(key);
        
        var fileDict = _branchHits.GetOrAdd(filePath, _ => new ConcurrentDictionary<string, int>());
        fileDict.AddOrUpdate($"{lineNumber}:{branchType}", 1, (_, count) => count + 1);
    }
    
    public CoverageReport CaptureCoverage()
    {
        return GenerateCoverageReport(_coveredLines.Count, _coveredBranches.Count);
    }
    
    public CoverageReport CaptureCoverageForAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name ?? "Unknown";
        
        var report = new CoverageReport
        {
            AssemblyName = assemblyName,
            Timestamp = DateTime.UtcNow,
            HitCounts = _lineHits
                .Where(kv => kv.Key.Contains(assemblyName) || Path.GetDirectoryName(kv.Key)?.Contains(assemblyName) == true)
                .SelectMany(kv => kv.Value.Select(line => new KeyValuePair<string, int>($"{kv.Key}:{line.Key}", line.Value)))
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        };
        
        var totalLines = EstimateTotalLines(assembly);
        var totalBranches = EstimateTotalBranches(assembly);
        
        var coveredLines = CountCoveredLines(assembly);
        var coveredBranches = CountCoveredBranches(assembly);
        
        report.TotalLines = totalLines;
        report.CoveredLines = coveredLines;
        report.TotalBranches = totalBranches;
        report.CoveredBranches = coveredBranches;
        
        foreach (var fileKv in _lineHits)
        {
            if (IsAssemblyFile(fileKv.Key, assembly))
            {
                var fileCoverage = new FileCoverage
                {
                    FilePath = fileKv.Key,
                    TotalLines = EstimateFileLines(fileKv.Key),
                    CoveredLines = fileKv.Value.Count,
                    LineHits = fileKv.Value.ToDictionary(kv => kv.Key, kv => kv.Value),
                    UncoveredLineNumbers = GetUncoveredLines(fileKv.Key, fileKv.Value.Keys.ToList())
                };
                report.Files[fileKv.Key] = fileCoverage;
            }
        }
        
        report.UncoveredLines = IdentifyUncoveredGaps(assembly);
        report.BranchCoverage = IdentifyBranchCoverage(assembly);
        
        return report;
    }
    
    public void Reset()
    {
        _lineHits.Clear();
        _branchHits.Clear();
        _coveredLines.Clear();
        _coveredBranches.Clear();
        _baseline = null;
    }
    
    public void SetBaseline(CoverageReport baseline)
    {
        _baseline = baseline;
    }
    
    public bool IsInteresting(CoverageReport newCoverage, CoverageReport baseline)
    {
        var newLines = newCoverage.CoveredLines - baseline.CoveredLines;
        var newBranches = newCoverage.CoveredBranches - baseline.CoveredBranches;
        
        return newLines > 0 || newBranches > 0;
    }
    
    public List<CoverageGap> GetNewGaps(CoverageReport baseline, CoverageReport current)
    {
        var newGaps = new List<CoverageGap>();
        
        foreach (var gap in current.UncoveredLines)
        {
            if (baseline.UncoveredLines.All(g => g.FilePath != gap.FilePath || g.LineNumber != gap.LineNumber))
            {
                newGaps.Add(gap);
            }
        }
        
        return newGaps;
    }
    
    private CoverageReport GenerateCoverageReport(int coveredLines, int coveredBranches)
    {
        return new CoverageReport
        {
            Timestamp = DateTime.UtcNow,
            CoveredLines = coveredLines,
            CoveredBranches = coveredBranches,
            HitCounts = _lineHits
                .SelectMany(kv => kv.Value.Select(line => new KeyValuePair<string, int>($"{kv.Key}:{line.Key}", line.Value)))
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }
    
    private int EstimateTotalLines(Assembly assembly)
    {
        return _lineHits.Sum(kv => kv.Value.Count) + EstimateUncoveredLines(assembly);
    }
    
    private int EstimateTotalBranches(Assembly assembly)
    {
        return _branchHits.Sum(kv => kv.Value.Count) + EstimateUncoveredBranches(assembly);
    }
    
    private int EstimateUncoveredLines(Assembly assembly)
    {
        var baseCount = 100;
        foreach (var module in assembly.GetLoadedModules())
        {
            try
            {
                var types = module.GetTypes();
                foreach (var type in types)
                {
                    if (type.IsClass && !type.IsAbstract)
                    {
                        baseCount += type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                            .Sum(m => m.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0) / 4;
                    }
                }
            }
            catch
            {
            }
        }
        return baseCount;
    }
    
    private int EstimateUncoveredBranches(Assembly assembly)
    {
        return (int)(EstimateUncoveredLines(assembly) * 0.15);
    }
    
    private int CountCoveredLines(Assembly assembly)
    {
        return _coveredLines.Count;
    }
    
    private int CountCoveredBranches(Assembly assembly)
    {
        return _coveredBranches.Count;
    }
    
    private bool IsAssemblyFile(string filePath, Assembly assembly)
    {
        var assemblyDir = Path.GetDirectoryName(assembly.Location);
        if (string.IsNullOrEmpty(assemblyDir)) return false;
        
        var fileDir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(fileDir)) return false;
        
        return fileDir.StartsWith(assemblyDir, StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains(assembly.GetName().Name ?? "");
    }
    
    private int EstimateFileLines(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                return File.ReadAllLines(filePath).Length;
            }
        }
        catch
        {
        }
        
        var fileName = Path.GetFileName(filePath);
        return _lineHits.TryGetValue(filePath, out var hits) ? hits.Count * 3 : 50;
    }
    
    private List<int> GetUncoveredLines(string filePath, List<int> coveredLines)
    {
        var uncovered = new List<int>();
        var totalLines = EstimateFileLines(filePath);
        
        var coveredSet = new HashSet<int>(coveredLines);
        for (int i = 1; i <= totalLines; i++)
        {
            if (!coveredSet.Contains(i))
            {
                uncovered.Add(i);
            }
        }
        
        return uncovered;
    }
    
    private List<CoverageGap> IdentifyUncoveredGaps(Assembly assembly)
    {
        var gaps = new List<CoverageGap>();
        var assemblyDir = Path.GetDirectoryName(assembly.Location) ?? "";
        
        foreach (var fileKv in _lineHits)
        {
            var filePath = fileKv.Key;
            var coveredLines = fileKv.Value.Keys.ToHashSet();
            
            try
            {
                if (File.Exists(filePath))
                {
                    var lines = File.ReadAllLines(filePath);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var lineNum = i + 1;
                        if (!coveredLines.Contains(lineNum))
                        {
                            var gap = new CoverageGap
                            {
                                FilePath = filePath,
                                LineNumber = lineNum,
                                LineContent = lines[i].Trim(),
                                Reason = "Line not executed"
                            };
                            
                            var methodName = GetMethodNameForLine(filePath, lineNum);
                            gap.MethodName = methodName;
                            
                            gaps.Add(gap);
                        }
                    }
                }
            }
            catch
            {
            }
        }
        
        return gaps;
    }
    
    private string GetMethodNameForLine(string filePath, int lineNumber)
    {
        return $"MethodAtLine{lineNumber}";
    }
    
    private List<BranchCoverage> IdentifyBranchCoverage(Assembly assembly)
    {
        var branches = new List<BranchCoverage>();
        
        foreach (var fileKv in _branchHits)
        {
            foreach (var branchKv in fileKv.Value)
            {
                var parts = branchKv.Key.Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var lineNum))
                {
                    var branchType = parts.Length > 1 ? parts[1] : "Unknown";
                    
                    branches.Add(new BranchCoverage
                    {
                        FilePath = fileKv.Key,
                        LineNumber = lineNum,
                        BranchType = branchType,
                        IsCovered = true,
                        HitCount = branchKv.Value
                    });
                }
            }
        }
        
        return branches;
    }
}

public class InstrumentationHelper
{
    public static void InstrumentAssemblyForTracking(string assemblyPath, ICoverageTracker tracker)
    {
        try
        {
            var targetAssembly = Assembly.LoadFrom(assemblyPath);
            
            foreach (var type in targetAssembly.GetTypes())
            {
                if (type.IsClass && !type.IsAbstract)
                {
                    InstrumentType(type, tracker);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to instrument assembly: {ex.Message}");
        }
    }
    
    private static void InstrumentType(Type type, ICoverageTracker tracker)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            InstrumentMethod(method, tracker);
        }
    }
    
    private static void InstrumentMethod(MethodInfo method, ICoverageTracker tracker)
    {
        try
        {
            var body = method.GetMethodBody();
            if (body == null) return;
            
            var ilBytes = body.GetILAsByteArray();
            if (ilBytes == null) return;
            
            var declaringType = method.DeclaringType;
            if (declaringType == null) return;
            
            var sourceFile = GetSourceFile(method);
            if (string.IsNullOrEmpty(sourceFile))
            {
                sourceFile = $"{declaringType.Name}.cs";
            }
            
            var lineNumber = 1;
            foreach (var instruction in ParseIL(ilBytes))
            {
                if (instruction.IsLineMarker)
                {
                    lineNumber = instruction.LineNumber;
                    tracker.MarkLineHit(sourceFile, lineNumber);
                }
            }
        }
        catch
        {
        }
    }
    
    private static string GetSourceFile(MethodInfo method)
    {
        var debuggableAttribute = method.DeclaringType?
            .Assembly.GetCustomAttribute<System.Diagnostics.DebuggableAttribute>();
        
        if (debuggableAttribute != null)
        {
            return $"{method.DeclaringType?.Name}.cs";
        }
        
        return $"{method.Module.ScopeName}.cs";
    }
    
    private class ILInstruction
    {
        public int Offset { get; set; }
        public bool IsLineMarker { get; set; }
        public int LineNumber { get; set; }
        public short OpCode { get; set; }
    }
    
    private static List<ILInstruction> ParseIL(byte[] il)
    {
        var instructions = new List<ILInstruction>();
        var i = 0;
        var currentLine = 1;
        
        while (i < il.Length)
        {
            var instruction = new ILInstruction { Offset = i };
            
            var opcode = il[i];
            instruction.OpCode = opcode;
            
            if (opcode == 0x13 || opcode == 0x14 || opcode == 0x17 || opcode == 0x1D)
            {
                instruction.IsLineMarker = true;
                currentLine = BitConverter.ToInt32(il, i + 1);
                instruction.LineNumber = currentLine;
            }
            
            i += 1 + (opcode == 0xFE ? 2 : 1);
            if (i < il.Length && il[i - 1] == 0xFE)
            {
                i += 4;
            }
            else if (i < il.Length)
            {
                i += GetOperandSize(opcode);
            }
            
            instructions.Add(instruction);
        }
        
        return instructions;
    }
    
    private static int GetOperandSize(short opcode)
    {
        return opcode switch
        {
            0x1C => 1,
            0x1D => 4,
            0x20 => 4,
            0x21 => 8,
            0x22 => 4,
            0x23 => 8,
            0x25 => 8,
            0x28 => 4,
            0x29 => 8,
            0x2A => 4,
            0x2B => 4,
            0x2C => 1,
            0x2D => 1,
            0x30 => 4,
            0x45 => 1,
            0x46 => 1,
            0x56 => 1,
            0x57 => 1,
            0x58 => 1,
            0x59 => 1,
            0x5A => 1,
            0x5B => 1,
            0x5C => 1,
            0x5D => 1,
            0x5E => 1,
            0x5F => 1,
            0x99 => 4,
            0x9A => 4,
            0x9B => 4,
            0x9C => 4,
            0x9D => 4,
            0x9E => 4,
            0x9F => 4,
            _ => 0
        };
    }
}
