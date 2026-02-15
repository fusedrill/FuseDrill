using System;
using System.Collections.Generic;
using System.Linq;
using NJsonSchema;
using FuseDrill.Core;

namespace FuseDrill.Core.Generation;

public class SchemaEdgeCaseGenerator
{
    private readonly Random _random;
    
    public SchemaEdgeCaseGenerator(int seed = 1234567)
    {
        _random = new Random(seed);
    }
    
    public IEnumerable<object> GenerateEdgeCases(JsonSchemaProperty schema, string propertyName)
    {
        var edgeCases = new List<object>();
        edgeCases.AddRange(GenerateBoundaryValues(schema));
        edgeCases.AddRange(GenerateConstraintViolations(schema));
        return edgeCases;
    }
    
    public IEnumerable<object> GenerateBoundaryValues(JsonSchemaProperty schema)
    {
        var values = new List<object>();
        var type = schema.Type;
        
        if (type == JsonObjectType.Integer || type == JsonObjectType.Number)
        {
            values.AddRange(new object[] { 0, -1, 1, int.MinValue, int.MaxValue, 999999999 });
        }
        
        if (type == JsonObjectType.String)
        {
            values.AddRange(new object[] { "", "a", new string('a', 100), new string('a', 1000) });
        }
        
        if (type == JsonObjectType.Array)
        {
            values.AddRange(new object[] { Array.Empty<object>(), new object[] { null } });
        }
        
        return values.Distinct();
    }
    
    public IEnumerable<object> GenerateConstraintViolations(JsonSchemaProperty schema)
    {
        var violations = new List<object>();
        var type = schema.Type;
        
        if (type == JsonObjectType.String)
        {
            violations.AddRange(new object[]
            {
                "!@#$%^&*()", "UPPERCASE", "lowercase", "MiXeD CaSe",
                string.Join("", Enumerable.Repeat("ñ", 50)),
                "中文テスト", new string(' ', 100), "\"quotes\""
            });
        }
        
        if (type == JsonObjectType.Integer || type == JsonObjectType.Number)
        {
            violations.AddRange(new object[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity });
        }
        
        if (schema.Format == JsonFormatStrings.DateTime)
        {
            violations.AddRange(new object[] { "not a date", "2024-02-30", "" });
        }
        
        if (schema.Format == JsonFormatStrings.Email)
        {
            violations.AddRange(new object[] { "notanemail", "@missinglocal.com", "missing@domain.com" });
        }
        
        if (schema.Format == JsonFormatStrings.Uri)
        {
            violations.AddRange(new object[] { "not a uri", "javascript:alert('xss')" });
        }
        
        if (schema.Format == JsonFormatStrings.Guid)
        {
            violations.AddRange(new object[] { "not-a-guid", "" });
        }
        
        return violations;
    }
    
    public object GenerateForProperty(JsonSchemaProperty property)
    {
        if (property == null) return "default";
        
        var type = property.Type;
        if (type == JsonObjectType.Integer || type == JsonObjectType.Number)
            return (decimal)0;
        if (type == JsonObjectType.String)
            return "";
        if (type == JsonObjectType.Boolean)
            return false;
        if (type == JsonObjectType.Array)
            return Array.Empty<object>();
        if (type == JsonObjectType.Object)
            return new Dictionary<string, object>();
        return null;
    }

    public IEnumerable<ApiCall> GenerateEdgeCases(ApiCall apiCall)
    {
        var edgeCases = new List<ApiCall>();

        for (int paramIndex = 0; paramIndex < apiCall.RequestParameters.Count; paramIndex++)
        {
            var param = apiCall.RequestParameters[paramIndex];
            var edgeCaseValues = GenerateEdgeCaseValues(param).ToList();

            foreach (var edgeCaseValue in edgeCaseValues)
            {
                    var mutatedCall = new ApiCall
                    {
                        ApiCallOrderId = apiCall.ApiCallOrderId + 1000 + edgeCases.Count,
                        MethodName = apiCall.MethodName,
                        HttpMethod = apiCall.HttpMethod,
                        RequestParameters = apiCall.RequestParameters.Select((p, idx) => new ParameterValue
                        {
                            Name = p.Name,
                            Type = p.Type,
                            Value = idx == paramIndex ? edgeCaseValue : p.Value
                        }).ToList(),
                        Response = null!
                    };
                edgeCases.Add(mutatedCall);
            }
        }

        return edgeCases;
    }

    private IEnumerable<object> GenerateEdgeCaseValues(ParameterValue param)
    {
        var type = param.Type;
        var edgeCases = new List<object>();

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            edgeCases.AddRange(new object[] { 0, -1, 1, int.MinValue, int.MaxValue, 999999999 });
        }
        else if (type == typeof(double) || type == typeof(decimal) || type == typeof(float))
        {
            edgeCases.AddRange(new object[] { 0, -1, 1, double.MinValue, double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity });
        }
        else if (type == typeof(string))
        {
            edgeCases.AddRange(new object[] { "", "a", new string('a', 100), new string('a', 1000), "!@#$%^&*()", "UPPERCASE", "lowercase", "MiXeD CaSe", new string(' ', 100) });
        }
        else if (type == typeof(bool))
        {
            edgeCases.AddRange(new object[] { true, false });
        }
        else if (type.IsArray || type == typeof(Array))
        {
            edgeCases.Add(Array.Empty<object>());
            edgeCases.Add(new object[] { null });
        }
        else if (type.IsClass)
        {
            edgeCases.Add(null);
        }

        return edgeCases.Distinct();
    }
}
