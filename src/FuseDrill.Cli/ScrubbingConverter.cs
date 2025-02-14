using System.Text.Json;
using System.Text.Json.Serialization;


public static class SerializerOptions
{
    public static JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = {
                new DateTimeScrubbingConverter(),
                new GuidScrubbingConverter(),
                new DateTimeOffsetScrubbingConverter(),
                new TypeJsonConverter() 
            }
        };
    }
}

public class TypeJsonConverter : JsonConverter<Type>
{
    public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Read the string value and convert it back to Type
        var typeName = reader.GetString();
        return Type.GetType(typeName) ?? throw new JsonException($"Could not find type: {typeName}");
    }

    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
    {
        // Write the type name as a string
        writer.WriteStringValue(value.Name);
    }
}
public class GuidScrubbingConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<Guid>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue("ScrubbedGuid");
    }
}
public class DateTimeScrubbingConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<DateTime>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue("ScrubbedDateTime");
    }

}
public class DateTimeOffsetScrubbingConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<DateTimeOffset>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue("ScrubbedDateTimeOffset");
    }

}
