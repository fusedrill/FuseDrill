﻿using Fuzzer;
using OneOf;
using System.Diagnostics;
using System.Reflection;

namespace tests.Fuzzer;

public class DataGenerationHelper
{
    public System.Random random { get; set; }
    public int _seed { get; set; }

    public RecursionGuard recursionGuard { get; set; } = new RecursionGuard();

    public DataGenerationHelper(int seed)
    {
        _seed = seed;
        random = new System.Random(seed);
    }

    public byte[] GetRandomBytes()
    {
        var bytes = new byte[16];
        random.NextBytes(bytes);
        return bytes;
    }

    public object PickCorrectRequestReflectionBased(string methodName, List<OneOf<Type, VoidEmptyType>> methodParameterTypes, int permutationSizeCount)
    {
        //do select on methodParameterTypes and create instance with random values
        var allRes = methodParameterTypes
            .Select(type => CreateInstanceWithRandomValues(type, permutationSizeCount)).ToList();

        if (allRes.Count == 1)
        {
            return allRes.First();
        }

        if (allRes.Count == 0)
        {
            return CreateInstanceWithRandomValues(OneOf<Type, VoidEmptyType>.FromT1(new VoidEmptyType()), permutationSizeCount);
        }

        return allRes;
    }

    static bool isPrimitiveType(Type type)
    {
        // Check if the type is a primitive type, or a special type that is treated as a primitive type
        return type.IsPrimitive // Checks for built-in primitive types like int, float, etc.
            || type == typeof(string) // String is commonly treated as a "primitive"
            || type == typeof(decimal) // Decimal is not considered a primitive by .NET, but should be handled
            || type == typeof(DateTime) // DateTime is also commonly treated as a simple type
            || type == typeof(Guid) // Include Guid as a simple type
            || type == typeof(Int64) // Include Guid as a simple type
            || type == typeof(DateTime) // Include Guid as a simple type
            || type == typeof(DateTimeOffset) // Include Guid as a simple type
            || type == typeof(long) // Include Guid as a simple type

            //|| type == typeof(string?) // String is commonly treated as a "primitive" ????????????
            || type == typeof(decimal?) // Decimal is not considered a primitive by .NET, but should be handled
            || type == typeof(DateTime?) // DateTime is also commonly treated as a simple type
            || type == typeof(Guid?) // Include Guid as a simple type
            || type == typeof(Int64?) // Include Guid as a simple type
            || type == typeof(DateTime?) // Include Guid as a simple type
            || type == typeof(DateTimeOffset?) // Include Guid as a simple type
            || type == typeof(long?); // Include Guid as a simple type
    }

    private object CreateInstanceWithRandomValues(OneOf<Type, VoidEmptyType> methodtype, int permutationSizeCount)
    {
        var instance = methodtype.Match(type => createInstance(type, permutationSizeCount), voidType => null);

        return instance;

        object createInstance(Type type, int permutationSizeCount)
        {
            var randomValue = CreateRandomValue(type, permutationSizeCount);
            return randomValue;
        }
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
        guidBytes[7] &= (byte)0x0F;  // Clear the top 4 bits of the 8th byte
        guidBytes[7] |= (byte)0x40;  // Set version to 4 (random-based)
        guidBytes[8] &= (byte)0x3F;  // Clear the top 2 bits of the 9th byte
        guidBytes[8] |= (byte)0x80;  // Set variant to RFC4122

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

#pragma warning disable CS8603 // Possible null reference return., thats expected because of nullables
       
        //Todo improve determistic behaviour
        var res = type switch
        {
            // Handle non-nullable types
            Type t when t == typeof(int) => random.Next(1, permutationSizeCount),                      // Random integer
            Type t when t == typeof(bool) => random.Next(0, 2) == 0,                                   // Random boolean
            Type t when t == typeof(double) => random.NextDouble() * 100,                              // Random double
            Type t when t == typeof(DateTime) => DateTime.Now.AddDays(random.Next(-100, 100)),         // Random date
            Type t when t == typeof(DateTimeOffset) => DateTimeOffset.Now.AddDays(random.Next(-100, 100)),         // Random date
            Type t when t == typeof(Guid) => GenerateGuidFromSeed(),
            Type t when t == typeof(Int64) => random.NextInt64(1, 10000),
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
            Type t when t == typeof(int?) => random.Next(0, 2) == 0 ? (int?)null : (int?)random.Next(1, permutationSizeCount),  // Random int? (null or int)
            Type t when t == typeof(bool?) => random.Next(0, 2) == 0 ? (bool?)null : random.Next(0, 2) == 0,               // Random bool? (null or bool)
            Type t when t == typeof(double?) => random.Next(0, 2) == 0 ? (double?)null : random.NextDouble() * 100,          // Random double? (null or double)
            Type t when t == typeof(DateTime?) => random.Next(0, 2) == 0 ? (DateTime?)null : DateTime.Now.AddDays(random.Next(-100, 100)), // Random DateTime? (null or DateTime)
            Type t when t == typeof(DateTimeOffset?) => random.Next(0, 2) == 0 ? (DateTimeOffset?)null : DateTimeOffset.Now.AddDays(random.Next(-100, 100)), // Random DateTime? (null or DateTime)
            Type t when t == typeof(Guid?) => random.Next(0, 2) == 0 ? (Guid?)null : GenerateGuidFromSeed(),
            Type t when t == typeof(long?) => random.Next(0, 2) == 0 ? (long?)null : random.NextInt64(1, 10000),
            Type t when t == typeof(float?) => random.Next(0, 2) == 0 ? (float?)null : random.NextSingle(),
            Type t when t == typeof(double?) => random.Next(0, 2) == 0 ? (double?)null : random.NextDouble(),
            Type t when t == typeof(decimal?) => random.Next(0, 2) == 0 ? (decimal?)null : random.NextDouble(),
            Type t when t == typeof(byte[]) => random.Next(0, 2) == 0 ? (byte[]?)null : GetRandomBytes(),

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
    private object CreateCollectionWithThreeElements(Type collectionType, int permutationSizeCount)
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

            // Use reflection to invoke the Add method on the dictionary
            dictionary.GetType().GetMethod("Add").Invoke(dictionary, new object[] { key, value });
        }

        return dictionary;
    }


    // Helper method to get a random value from an enum type
    private object GetRandomEnumValue(Type enumType, System.Random random)
    {
        var values = Enum.GetValues(enumType);
        var randomIndex = random.Next(values.Length);

        // Check if the random index is within the range of valid enum values
        if (randomIndex >= values.Length)
        {
            throw new InvalidOperationException("Random index is out of range of valid enum values.");
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
        var listSuitePermutations = range.Count switch
        {
            > 50 => PermutationGenerator.GetPermutationsOfOne(range), // Skip permutations if count > 50
            > 5 => PermutationGenerator.GetPermutationsOfTwo(range), // If count > 5, use GetPermutationsOfTwo
            _ => PermutationGenerator.GetPermutations(range) // Otherwise, get full permutations
        };

        var permuations = listSuitePermutations.Select(set =>
        {
            var list = set.Zip(dataModel.Methods).Select((item, index) => new ApiCall
            {
                Order = item.First,
                HttpMethod = item.Second.HttpMethod,
                MethodName = item.Second.MethodName,
                Request = PickCorrectRequestReflectionBased(item.Second.MethodName, item.Second.MethodParameterTypes, dataModel.Methods.Count),
                Response = null,
            }).ToList();
            return list.ToList();
        }).ToList();

        var testSuites = permuations.Select(calls => new TestSuite
        {
            ApiCalls = calls.ToList(),
            TestCoveragePercentage = 0,
        }).ToList();

        //if (range.Count == 1) // To complicated, Todo Generate all permutations of property values, atlest for enums ,and bools.
        //{
        //    ///loop trhou all testsuites 
        //    ///     loop throut all api calls
        //    ///         find properties that are bool and set them to oposite

        //    foreach (var testSuite in testSuites)
        //    {
        //        foreach (var apiCall in testSuite.ApiCalls)
        //        {
        //            if (apiCall.Request is bool)
        //            {
        //                apiCall.Request = !apiCall.Request;
        //            }
        //        }
        //    }
        //}

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
