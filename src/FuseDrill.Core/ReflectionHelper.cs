using System.Reflection;

public static class ReflectionHelper
{
    /// <summary>
    /// Gets the names of all public instance methods.
    /// </summary>
    /// <param name="type">The type to reflect over.</param>
    /// <returns>A list of public method names that represent API endpoints.</returns>
    public static List<MethodInfo> GetPublicEndpointMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                   .Where(m => !m.IsSpecialName) // Exclude property getters/setters
                   .Where(m => IsApiEndpoint(m)) // Custom logic to filter endpoint methods
                   .OrderBy(m => m.Name)
                   .GroupBy(item => item.Name).Select(item => item.First()).ToList();
    }
    public static List<MethodInfo> GetPublicMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                   .Where(m => !m.IsSpecialName) // Exclude property getters/setters
                   .OrderBy(m => m.Name)
                   .GroupBy(item => item.Name).Select(item => item.First()).ToList();
    }

    /// <summary>
    /// Gets the public endpoint method by name.
    /// </summary>
    public static MethodInfo GetPublicEndpointMethodByName(Type type, string name)
    {
        return GetPublicEndpointMethods(type).FirstOrDefault(item => item.Name == name);
    }

    /// <summary>
    /// Determines if the method is an API endpoint based on its return type (Task or Task T).
    /// </summary>
    /// <param name="methodInfo">The method to check.</param>
    /// <returns>True if the method is an API endpoint, otherwise false.</returns>
    private static bool IsApiEndpoint(MethodInfo methodInfo)
    {
        var returnType = methodInfo.ReturnType;

        // Check if the method returns Task or Task<T>
        return returnType == typeof(Task) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>));
    }
}
