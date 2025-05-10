using System.Text.Json.Serialization;

namespace FuseDrill.MCP.Models;

public class Monkey
{
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? Details { get; set; }
    public string? Image { get; set; }
    public int Population { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(List<Monkey>))]
[JsonSerializable(typeof(Monkey))]
internal sealed partial class MonkeyContext : JsonSerializerContext
{
}
