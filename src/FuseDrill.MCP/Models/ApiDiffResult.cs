namespace FuseDrill.MCP.Models;

public class ApiDiffResult
{
    public int TotalChanges { get; set; }
    public int BreakingChanges { get; set; }
    public int AddedEndpoints { get; set; }
    public int RemovedEndpoints { get; set; }
    public int ModifiedEndpoints { get; set; }
    public IEnumerable<string> Details { get; set; } = new List<string>();
}
