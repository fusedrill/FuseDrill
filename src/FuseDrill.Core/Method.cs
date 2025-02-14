using System.Reflection;

namespace FuseDrill.Core;

public class Method
{
    public required string MethodName { get; set; }
    public string HttpMethod { get; set; }
    public MethodInfo MethodForCall { get; set; }
    public List<Parameter> MethodParameters { get; set; }
}

public class Parameter
{
    public Type Type { get; set; }
    public string Name { get; set; }
}

public class ParameterValue
{
    public object Value { get; set; }
    public Type Type { get; set; }
    public string Name { get; set; }
}