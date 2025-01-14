using OneOf;

namespace FuseDrill.Core;

public class ApiShapeData
{
    public List<Method> Methods { get; set; }

    public ApiShapeData(object ClientInstance)
    {
        var methods = ReflectionHelper.GetPublicEndpointMethods(ClientInstance.GetType()).OrderBy(item => item.Name).ToList();
        Methods = methods.Select(item => new Method
        {
            MethodName = item.Name,
            HttpMethod = ExtractTextBeeetween(item.Name, "_http_", "_"),
            MethodParameterTypeNames = item?.GetParameters()?.Select(item => item?.ParameterType?.Name ?? "None").ToList() ?? ["None"], // not sure what if this is good way lets see
            MethodParameterTypes = item?.GetParameters()?.Select(item => item?.ParameterType ?? OneOf<Type, VoidEmptyType>.FromT1(new VoidEmptyType())).ToList() ?? new List<OneOf<Type, VoidEmptyType>> { OneOf<Type, VoidEmptyType>.FromT1(new VoidEmptyType()) }
        }).ToList();
    }

    private string ExtractTextBeeetween(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start) + start.Length;
        int endIndex = text.IndexOf(end, startIndex);
        if (endIndex == -1)
        {
            return "";
        }
        return text.Substring(startIndex, endIndex - startIndex);
    }

}
