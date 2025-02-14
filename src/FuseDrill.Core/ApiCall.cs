using System.Text.Json.Serialization;

namespace FuseDrill.Core;



public class ApiCall
{
    public required string MethodName { get; set; }
    public int ApiCallOrderId { get; set; } = 0;
    public required List<ParameterValue> RequestParameters { get; set; }
    public required object Response { get; set; }
    public string HttpMethod { get; internal set; }

    //Do not serialize this property
    [JsonIgnore]
    public Method Method { get; set; }

}
