namespace FuseDrill.Core;

public class ApiCall
{
    public required string MethodName { get; set; }
    public int ApiCallOrderId { get; set; } = 0;
    public required object Request { get; set; }
    public required object Response { get; set; }
    public string HttpMethod { get; internal set; }
}
