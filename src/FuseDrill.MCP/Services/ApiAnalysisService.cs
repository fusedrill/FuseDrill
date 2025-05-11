using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace FuseDrill.MCP.Services;

public class ApiAnalysisService
{
    private readonly HttpClient _httpClient = new();

    public async Task<OpenApiDocument> LoadSpecification(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        using var stream = await response.Content.ReadAsStreamAsync();
        var reader = new OpenApiStreamReader();
        var result = await reader.ReadAsync(stream);
        return result.OpenApiDocument;
    }

    public async Task<IEnumerable<string>> CompareSpecs(string oldVersionUrl, string newVersionUrl)
    {
        var oldDoc = await LoadSpecification(oldVersionUrl);
        var newDoc = await LoadSpecification(newVersionUrl);

        var changes = new List<string>();

        // Compare paths
        foreach (var path in oldDoc.Paths.Keys.Union(newDoc.Paths.Keys))
        {
            if (!oldDoc.Paths.ContainsKey(path))
                changes.Add($"+{path}");
            else if (!newDoc.Paths.ContainsKey(path))
                changes.Add($"-{path}");
            else
            {
                var oldOps = oldDoc.Paths[path].Operations;
                var newOps = newDoc.Paths[path].Operations;
                CompareOperations(path, oldOps, newOps, changes);
            }
        }

        // Compare schemas
        CompareSchemas(oldDoc.Components?.Schemas, newDoc.Components?.Schemas, changes);

        return changes;
    }

    private void CompareOperations(string path, 
        IDictionary<OperationType, OpenApiOperation> oldOps,
        IDictionary<OperationType, OpenApiOperation> newOps,
        List<string> changes)
    {
        foreach (var op in oldOps.Keys.Union(newOps.Keys))
        {
            if (!oldOps.ContainsKey(op))
                changes.Add($"+{path} [{op}]");
            else if (!newOps.ContainsKey(op))
                changes.Add($"-{path} [{op}]");
            else
            {
                CompareParameters(path, op, oldOps[op].Parameters, newOps[op].Parameters, changes);
                CompareResponses(path, op, oldOps[op].Responses, newOps[op].Responses, changes);
            }
        }
    }

    private void CompareParameters(string path, OperationType op,
        IList<OpenApiParameter> oldParams,
        IList<OpenApiParameter> newParams,
        List<string> changes)
    {
        var oldSet = oldParams?.ToHashSet(new ParameterComparer()) ?? new();
        var newSet = newParams?.ToHashSet(new ParameterComparer()) ?? new();

        foreach (var param in oldSet.Union(newSet))
        {
            if (!oldSet.Contains(param))
                changes.Add($"+{path} [{op}] param: {param.Name}");
            else if (!newSet.Contains(param))
                changes.Add($"-{path} [{op}] param: {param.Name}");
        }
    }

    private void CompareResponses(string path, OperationType op,
        OpenApiResponses oldResps,
        OpenApiResponses newResps,
        List<string> changes)
    {
        foreach (var code in oldResps.Keys.Union(newResps.Keys))
        {
            if (!oldResps.ContainsKey(code))
                changes.Add($"+{path} [{op}] response: {code}");
            else if (!newResps.ContainsKey(code))
                changes.Add($"-{path} [{op}] response: {code}");
        }
    }

    private void CompareSchemas(
        IDictionary<string, OpenApiSchema> oldSchemas,
        IDictionary<string, OpenApiSchema> newSchemas,
        List<string> changes)
    {
        if (oldSchemas == null || newSchemas == null) return;

        foreach (var schema in oldSchemas.Keys.Union(newSchemas.Keys))
        {
            if (!oldSchemas.ContainsKey(schema))
                changes.Add($"+schema: {schema}");
            else if (!newSchemas.ContainsKey(schema))
                changes.Add($"-schema: {schema}");
            else
            {
                CompareSchemaProperties(schema, oldSchemas[schema], newSchemas[schema], changes);
            }
        }
    }

    private void CompareSchemaProperties(string schema,
        OpenApiSchema oldSchema,
        OpenApiSchema newSchema,
        List<string> changes)
    {
        var oldProps = oldSchema.Properties ?? new Dictionary<string, OpenApiSchema>();
        var newProps = newSchema.Properties ?? new Dictionary<string, OpenApiSchema>();

        foreach (var prop in oldProps.Keys.Union(newProps.Keys))
        {
            if (!oldProps.ContainsKey(prop))
                changes.Add($"+schema: {schema}.{prop}");
            else if (!newProps.ContainsKey(prop))
                changes.Add($"-schema: {schema}.{prop}");
            else if (oldProps[prop].Type != newProps[prop].Type)
                changes.Add($"~schema: {schema}.{prop} type: {oldProps[prop].Type} => {newProps[prop].Type}");
        }
    }
}

public class ParameterComparer : IEqualityComparer<OpenApiParameter>
{
    public bool Equals(OpenApiParameter? x, OpenApiParameter? y)
    {
        if (x == null && y == null) return true;
        if (x == null || y == null) return false;
        return x.Name == y.Name && x.In == y.In;
    }

    public int GetHashCode(OpenApiParameter obj)
    {
        if (obj == null) return 0;
        return HashCode.Combine(obj.Name, obj.In);
    }
}
