using System.Reflection;

namespace FuseDrill.Core;

public class ApiShapeData
{
    public List<Method> Methods { get; set; }

    public ApiShapeData(object ClientInstance)
    {
        var methods = ReflectionHelper.GetPublicMethods(ClientInstance.GetType()).OrderBy(item => item.Name).ToList();
        //var methods = ReflectionHelper.GetPublicEndpointMethods(ClientInstance.GetType()).OrderBy(item => item.Name).ToList();
        Methods = methods.Select(item => new Method
        {
            MethodName = item.Name,
            MethodForCall = item,
            HttpMethod = ExtractTextBeeetween(item.Name, "_http_", "_"),
            MethodParameters = GetParameters(item)
        }).ToList();
    }

    private static List<Parameter> GetParameters(MethodInfo item)
    {
        var parameters = item?.GetParameters()?
            .Select(item => new Parameter { Name = item?.Name, Type = item?.ParameterType })
            .ToList() ?? new List<Parameter> { };

        return parameters;
    }

    private string ExtractTextBeeetween(string text, string start, string end)
    {
        // Check if the start and end strings are present in the text using contains.
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end) ||
            !text.Contains(start) || !text.Contains(end))
        {
            return "";
        }

        int startIndex = text.IndexOf(start) + start.Length;
        int endIndex = text.IndexOf(end, startIndex);
        if (endIndex == -1)
        {
            return "";
        }
        return text.Substring(startIndex, endIndex - startIndex);
    }

}
