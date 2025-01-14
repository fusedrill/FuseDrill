using FuseDrill.Core;
using System.Collections;
using System.Reflection;
using System.Text;

public class FuzzerTests
{
    public int Seed { get; set; }
    public List<TestSuite> TestSuites { get; set; }
}

public class TestSuite
{
    public List<ApiCall> ApiCalls { get; set; } = [];
    public double TestCoveragePercentage { get; set; } = 0;
    public int TestSuiteOrderId { get; set; } = 0;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("TestSuite");

        foreach (var apiCall in ApiCalls)
        {
            sb.AppendLine($"   ├── {GetEmojiForMethod(apiCall.MethodName)} {apiCall.MethodName}");
            sb.AppendLine($"   │    └── {GetResponseHeader(apiCall.Response)}");
            sb.AppendLine(RenderObject(apiCall.Response));
            if (apiCall.Request != null)
            {
                sb.AppendLine($"   │    └── {GetRequestHeader(apiCall.Request)}");
                sb.AppendLine(RenderObject(apiCall.Request));
            }
        }

        return sb.ToString();
    }

    private string GetEmojiForMethod(string methodName)
    {
        return methodName switch
        {
            "GetWeatherForecastAsync" => "🌤️",
            "PetsAllAsync" => "🐾",
            "PetsGETAsync" => "🐶",
            "PetsPOSTAsync" => "✍️",
            "PetsPUTAsync" => "🔄",
            _ => "❓" // Fallback emoji
        };
    }

    private string GetResponseHeader(object response)
    {
        return response switch
        {
            Array => "📭 Response: []",
            _ => "📅 Response:"
        };
    }

    private string RenderObject(object @object)
    {
        var sb = new StringBuilder();

        if (@object is Array responseArray && responseArray.Length == 0)
            return sb.ToString();

        // Use reflection to inspect the properties of the response object

        if (@object is IList list)
        {
            foreach (var item in list)
            {
                renderProp(sb, item);
            }
        }
        else
        {
            renderProp(sb, @object);
        }

        return sb.ToString(); // Handle other response types as needed

        static void renderProp(StringBuilder sb, object? item)
        {
            if (item != null)
            {
                foreach (PropertyInfo prop in item.GetType().GetProperties())
                {
                    var value = prop.GetValue(item);
                    sb.Append($"   │        {prop.Name,-40}");
                }
                sb.AppendLine();
                foreach (PropertyInfo prop in item.GetType().GetProperties())
                {
                    var value = prop.GetValue(item);
                    sb.Append($"   │        {value,-40}");
                }
                sb.AppendLine();
            }
        }
    }

    private string GetRequestHeader(object request)
    {
        return request switch
        {
            Dictionary<string, object> => "📤 Request:",
            _ => "📤 Request:"
        };
    }

}
