using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;

namespace FuseDrill.Core;

public class DataGenerationHelper
{
    public Random random { get; set; }
    public int _seed { get; set; }

    public RecursionGuard recursionGuard { get; set; } = new RecursionGuard();

    public DataGenerationHelper(int seed)
    {
        _seed = seed;
        random = new Random(seed);
    }

    public byte[] GetRandomBytes()
    {
        var bytes = new byte[16];
        random.NextBytes(bytes);
        return bytes;
    }

    public List<ParameterValue> PickCorrectRequestReflectionBased(string methodName, List<Parameter> methodParameters, int permutationSizeCount)
    {
        var allRes = methodParameters.Select(parameter => CreateResposnseParameterWithValue(parameter, permutationSizeCount)).ToList();

        return allRes;
    }

    private ParameterValue CreateResposnseParameterWithValue(Parameter methodParameter, int permutationSizeCount)
    {
        var instance = new ParameterValue
        {
            Value = CreateRandomValue(methodParameter.Type, permutationSizeCount),
            Name = methodParameter.Name,
            Type = methodParameter.Type,
        };

        return instance;
    }

    private object CreateClassInstance(Type type, int permutationSizeCount)
    {
        var instance = Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Cannot create instance of type {type}");

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Set random values to the properties
        foreach (var property in properties)
        {
            if (!property.CanWrite) continue; // Skip read-only properties
            var randomValue = CreateRandomValue(property.PropertyType, permutationSizeCount);
            property.SetValue(instance, randomValue);
        }

        //debug assert if type create is in good type type shape, should work with nullables
        Debug.Assert(instance.GetType().FullName == type.FullName, $"Instance of type {type} should match the type that we want to create");
        return instance;
    }

    public Guid GenerateGuidFromSeed()
    {
        // Create a random number generator with the given seed

        // Create an array of 16 bytes (128 bits for the GUID)
        byte[] guidBytes = new byte[16];

        // Fill the array with random values
        random.NextBytes(guidBytes);

        // Set the version and variant fields of the GUID as per the specification
        guidBytes[7] &= 0x0F;  // Clear the top 4 bits of the 8th byte
        guidBytes[7] |= 0x40;  // Set version to 4 (random-based)
        guidBytes[8] &= 0x3F;  // Clear the top 2 bits of the 9th byte
        guidBytes[8] |= 0x80;  // Set variant to RFC4122

        // Create the GUID from the byte array
        return new Guid(guidBytes);
    }

    public Uri CreateMockedUri()
    {
        return new Uri(@$"https://{"RandomString" + random.Next(1, 1000)}.com");
    }

    public Stream CreateMockedStream()
    {
        Stream stream = new MemoryStream(GetRandomBytes());
        return stream;
    }

    private object CreateRandomValue(Type type, int permutationSizeCount)
    {

        if (!recursionGuard.TryEnter(type))
        {
            return GetDefaultValue(type);  // Return empty or default value if recursion limit reached
        }

        //Todo improve deterministic behaviour uisng random seed as key : MethodName + type.FullName + propertyName
        var res = type switch
        {
            // Handle non-nullable types
            Type t when t == typeof(int) => random.Next(1, permutationSizeCount),                      // Random integer
            Type t when t == typeof(bool) => random.Next(0, 2) == 0,                                   // Random boolean
            Type t when t == typeof(double) => random.NextDouble() * 100,                              // Random double
            Type t when t == typeof(DateTime) => DateTime.Now.AddDays(random.Next(-100, 100)),         // Random date
            Type t when t == typeof(TimeSpan) => TimeSpan.FromHours(random.Next(1, 100)),              // Random TimeSpan
            Type t when t == typeof(DateTimeOffset) => DateTimeOffset.Now.AddDays(random.Next(-100, 100)),         // Random date
            Type t when t == typeof(Guid) => GenerateGuidFromSeed(),
            Type t when t == typeof(long) => random.NextInt64(1, 10000),
            Type t when t == typeof(float) => random.NextSingle(),
            Type t when t == typeof(double) => random.NextDouble(),
            Type t when t == typeof(decimal) => random.NextDouble(),
            Type t when t == typeof(byte[]) => GetRandomBytes(),


            // Handle non-nullable string
            Type t when t == typeof(string) => "RandomString" + random.Next(1, 1000),  // Non-nullable string (always returns a string)

            // Handle nullable reference types (e.g., string?)
            Type t when t == typeof(string) && Nullable.GetUnderlyingType(t) != null => random.Next(0, 2) == 0
                ? null
                : "RandomString" + random.Next(1, 1000),  // Random string? (null or string)

            // Handle nullable types explicitly (e.g., int?, bool?, double?, DateTime?)
            Type t when t == typeof(int?) => random.Next(0, 2) == 0 ? null : (int?)random.Next(1, permutationSizeCount),  // Random int? (null or int)
            Type t when t == typeof(bool?) => random.Next(0, 2) == 0 ? (bool?)null : random.Next(0, 2) == 0,               // Random bool? (null or bool)
            Type t when t == typeof(double?) => random.Next(0, 2) == 0 ? (double?)null : random.NextDouble() * 100,          // Random double? (null or double)
            Type t when t == typeof(DateTime?) => random.Next(0, 2) == 0 ? (DateTime?)null : DateTime.Now.AddDays(random.Next(-100, 100)), // Random DateTime? (null or DateTime)
            Type t when t == typeof(TimeSpan?) => random.Next(0, 2) == 0 ? (TimeSpan?)null : TimeSpan.FromHours(random.Next(1, 100)), // Random TimeSpan? (null or DateTime)
            Type t when t == typeof(DateTimeOffset?) => random.Next(0, 2) == 0 ? (DateTimeOffset?)null : DateTimeOffset.Now.AddDays(random.Next(-100, 100)), // Random DateTime? (null or DateTime)
            Type t when t == typeof(Guid?) => random.Next(0, 2) == 0 ? (Guid?)null : GenerateGuidFromSeed(),
            Type t when t == typeof(long?) => random.Next(0, 2) == 0 ? (long?)null : random.NextInt64(1, 10000),
            Type t when t == typeof(float?) => random.Next(0, 2) == 0 ? (float?)null : random.NextSingle(),
            Type t when t == typeof(double?) => random.Next(0, 2) == 0 ? (double?)null : random.NextDouble(),
            Type t when t == typeof(decimal?) => random.Next(0, 2) == 0 ? null : random.NextDouble(),
            Type t when t == typeof(byte[]) => random.Next(0, 2) == 0 ? null : GetRandomBytes(),

            //Mocking rest api files parameter
            Type t when t == typeof(FileParameter) =>
                FileParameter.CreateMockedFile(),

            //Mocking uri
            Type t when t == typeof(Uri) =>
                CreateMockedUri(),

            //Mocking stream
            Type t when t == typeof(Stream) =>
                CreateMockedStream(),

            //For self mocking
            Type t when t == typeof(Func<ApiCall, bool>) => null,


            //Handle complex nullable types
            Type t when MyTypeExtensions.IsNullableOfT(t) => Activator.CreateInstance(t, null),

            // Handle enums by randomly selecting one of the possible values
            Type t when t.IsEnum => GetRandomEnumValue(t, random),

            // Handle IEnumerable<T> types (always create 3 elements)
            Type t when t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>) =>
                CreateEnumerableWithThreeElements(t, permutationSizeCount),  // Delegate to a helper method

            // Handle ICollection<T> types (always create 3 elements)
            Type t when t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>) =>
                CreateICollectionWithThreeElements(t, permutationSizeCount),  // Delegate to a helper method

            // Handle List<T> types (always create 3 elements)
            Type t when t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) =>
                CreateICollectionWithThreeElements(t, permutationSizeCount),  // Delegate to a helper method

            // Handle Collection<T> types (always create 3 elements)
            Type t when t.BaseType != null && t.BaseType.IsGenericType && t.BaseType.GetGenericTypeDefinition() == typeof(Collection<>) =>
                CreateCollectionWithThreeElements(t, permutationSizeCount),  // Delegate to a helper method

            // Handle IDictionary<TKey, TValue> types (always create 3 key-value pairs)
            Type t when t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>) =>
                CreateDictionaryWithThreeEntries(t, permutationSizeCount),  // Delegate to a helper method

            // For complex types, recursively create an instance with random values
            Type t when t.IsClass =>
                CreateClassInstance(t, permutationSizeCount),

            // If the type is not supported, throw
            Type t => throw new InvalidOperationException($@"Cant create '{t.ToString()}'")
        };
#pragma warning restore CS8603 // Possible null reference return.

        recursionGuard.Exit(type);

        if (MyTypeExtensions.IsNullableOfT(type))
        {
            var underlyinType = Nullable.GetUnderlyingType(type);
            //if type is value type
            if (underlyinType?.IsValueType == true)
            {
                Debug.Assert(res == null || res.GetType() == underlyinType, $"Value should be null or of type {underlyinType.FullName}");
            }
            //is nullable of complex type like class
            else
            {
                Debug.Assert(res?.GetType()?.FullName == type.FullName, $"Instance of type {type} should match the type that we want to create");
            }
        }

        return res;
    }

    public static class MyTypeExtensions
    {
        public static bool IsNullableOfT(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }

    // Helper method to create a collection with exactly 3 random elements
    private object CreateEnumerableWithThreeElements(Type collectionType, int permutationSizeCount)
    {

        // Get the type of the elements in the collection
        Type elementType = collectionType.GetGenericArguments()[0];

        // Create a list to store the elements
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType);

        // Add three elements to the list
        for (int i = 0; i < 3; i++)
        {
            var value = CreateRandomValue(elementType, permutationSizeCount);
            list.Add(value);
        }

        // If the requested collection type is IEnumerable<T>, just return the list
        if (collectionType.IsAssignableFrom(listType))
        {
            return list;
        }

        // Attempt to create an instance of the desired collection type with the elements
        try
        {
            return Activator.CreateInstance(collectionType, list);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Cannot create an instance of type '{collectionType}' from the given elements.", ex);
        }
    }

    // Helper method to create a collection with exactly 3 random elements
    private object CreateICollectionWithThreeElements(Type collectionType, int permutationSizeCount)
    {
        try
        {
            // Get the element type (T) of the collection
            Type elementType = collectionType.GetGenericArguments()[0];

            // Create a List<T> with the correct element type
            var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));

            // Add exactly 3 random elements to the collection
            for (int i = 0; i < 3; i++)
            {
                // Create a random value of the element type and add it to the collection
                list.GetType().GetMethod("Add").Invoke(list, new object[] { CreateRandomValue(elementType, permutationSizeCount) });
            }

            return list;
        }
        catch (Exception)
        {

            throw;
        }
    }

    // Helper method to create a collection with exactly 3 random elements
    private object CreateCollectionWithThreeElements(Type collectionType, int permutationSizeCount)
    {
        try
        {
            // Get the element type (T) of the collection
            Type elementType = collectionType.BaseType.GetGenericArguments()[0];

            // Create a List<T> with the correct element type
            var list = Activator.CreateInstance(collectionType);

            // Add exactly 3 random elements to the collection
            for (int i = 0; i < 3; i++)
            {
                // Create a random value of the element type and add it to the collection
                list.GetType().GetMethod("Add").Invoke(list, new object[] { CreateRandomValue(elementType, permutationSizeCount) });
            }

            return list;
        }
        catch (Exception)
        {

            throw;
        }
    }

    private object GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            // If it's a value type (like int, bool), use Activator to create the default instance (e.g., 0 for int, false for bool)
            return Activator.CreateInstance(type);
        }
        else
        {
            // For reference types, return null
            return null;
        }
    }

    // Helper method to create a dictionary with exactly 3 random key-value pairs
    private object CreateDictionaryWithThreeEntries(Type dictionaryType, int permutationSizeCount)
    {
        // Get the key and value types
        Type[] genericArguments = dictionaryType.GetGenericArguments();
        Type keyType = genericArguments[0];
        Type valueType = genericArguments[1];

        // Create a new dictionary instance with the specified key and value types
        var dictionary = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType));

        // Add exactly 3 random key-value pairs to the dictionary
        for (int i = 0; i < 3; i++)
        {
            // Generate random key and value
            var key = CreateRandomValue(keyType, permutationSizeCount);
            var value = CreateRandomValue(valueType, permutationSizeCount);

            // Get dictionary type
            var dictType = dictionary.GetType();
            var containsMethod = dictType.GetMethod("ContainsKey");

            bool keyExists = (bool)containsMethod.Invoke(dictionary, new object[] { key });

            if (!keyExists)
            {
                // Use reflection to invoke the Add method on the dictionary
                dictType.GetMethod("Add").Invoke(dictionary, new object[] { key, value });
            }
        }

        return dictionary;
    }

    // Helper method to get a random value from an enum type
    private object GetRandomEnumValue(Type enumType, Random random)
    {
        var values = Enum.GetValues(enumType);
        var randomIndex = random.Next(values.Length);

        // Check if the random testSuiteOrder is within the range of valid enum values
        if (randomIndex >= values.Length)
        {
            throw new InvalidOperationException("Random testSuiteOrder is out of range of valid enum values.");
        }

        var result = values.GetValue(randomIndex);
        Debug.Assert(result.GetType().FullName == enumType.FullName, $"Instance of type {enumType} should match the type that we want to create");
        return result;
    }

    public List<TestSuite> CreateApiMethodPermutationsTestSuite(ApiShapeData dataModel)
    {

        var range = Enumerable.Range(1, dataModel.Methods.Count).ToList();

        //What is the average HTTP endpoint count in typical micro-service API?
        //The number of HTTP endpoints in a typical micro-service API varies depending on the complexity and specific domain of the service
        //However, a common range for endpoints per micro-service is generally between 5 to 20 endpoints.

        //TODO How to reduce search space? Currently just a simple hack.
        var methodNamePermutations = range.Count switch
        {
            > 50 => PermutationGenerator.GetPermutationsOfOne(dataModel.Methods), // Skip permutations if count > 50
            > 5 => PermutationGenerator.GetPermutationsOfTwo(dataModel.Methods), // If count > 5, use GetPermutationsOfTwo
            _ => PermutationGenerator.GetPermutations(dataModel.Methods) // Otherwise, get full permutations
        };

        var testSuites = methodNamePermutations.Select((calls, testSuiteOrder) => new TestSuite
        {
            ApiCalls = calls.Select((item, apiCallOrder) => new ApiCall
            {
                ApiCallOrderId = apiCallOrder,
                HttpMethod = item.HttpMethod,
                MethodName = item.MethodName,
                Method = item,
                RequestParameters = PickCorrectRequestReflectionBased(item.MethodName, item.MethodParameters, dataModel.Methods.Count),
                Response = null,
            }).ToList(),
            TestSuiteOrderId = testSuiteOrder,
            TestCoveragePercentage = 0,
        }).ToList();

        return testSuites;
    }

}

public class RecursionGuard
{
    private const int MaxRecursionDepth = 5;  // Set a reasonable depth limit
    private readonly HashSet<Type> currentlyCreatingTypes = new HashSet<Type>();  // Track types currently being created

    public int Depth { get; private set; } = 0;

    // Enters the scope of a type, returns false if max depth is exceeded or type is already in process
    public bool TryEnter(Type type)
    {
        if (Depth >= MaxRecursionDepth || currentlyCreatingTypes.Contains(type))
        {
            return false;
        }

        Depth++;
        currentlyCreatingTypes.Add(type);
        return true;
    }

    // Exits the scope of a type
    public void Exit(Type type)
    {
        Depth--;
        currentlyCreatingTypes.Remove(type);
    }
}
