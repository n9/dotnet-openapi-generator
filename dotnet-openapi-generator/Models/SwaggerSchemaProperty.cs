using System.Text;

namespace dotnet.openapi.generator;

internal class SwaggerSchemaProperty
{
    [System.Text.Json.Serialization.JsonPropertyName("$ref")]
    public string? @ref { get; set; }
    public SwaggerSchemaType? type { get; set; }
    public string? format { get; set; }
    public object? @default { get; set; }
    public bool nullable { get; set; }
    //public bool? required { get; set; }

    [System.Text.Json.Serialization.JsonConverter(typeof(SwaggerSchemaPropertyAdditionalPropertiesConverter))]
    public SwaggerSchemaPropertyAdditionalProperties? additionalProperties { get; set; }
    public System.Text.Json.JsonElement? items { get; set; }

    public string GetBody(string name, bool supportRequiredProperties, string? jsonPropertyNameAttribute)
    {
        StringBuilder builder = new();

        bool startsWithDigit = char.IsDigit(name[0]);

        if (startsWithDigit && jsonPropertyNameAttribute is not null)
        {
            builder.Append('[')
                   .Append(jsonPropertyNameAttribute.Replace("{name}", name))
                   .Append(']');
        }

        builder.Append("public ");

        if (supportRequiredProperties && (!nullable /*|| required.GetValueOrDefault()*/))
        {
            builder.Append("required ");
        }

        builder.Append(ResolveType(nullable));

        builder.Append(' ');

        if (char.IsDigit(name[0]))
        {
            builder.Append('_');
        }

        builder.Append(name[0..1].ToUpperInvariant())
               .Append(name[1..])
               .Append(" { get; set; }");

        return builder.ToString().TrimEnd();
    }

    public string ResolveType(bool nullable = false)
    {
        var rt = ResolveTypeRaw(ref nullable);
        return ApplyNullable(rt, nullable) ?? "";
    }

    static string? ApplyNullable(string? type, bool nullable)
    {
        if (type is not null && nullable)
            type = $"{type}?";
        return type;
    }

    string? ResolveTypeRaw(ref bool nullable)
    {
        if (format is not null)
        {
            string? result = format.ResolveType(format.Contains("#/components/schemas/"), items, additionalProperties);
            if (result is not null)
            {
                return result;
            }
        }

        var r = (ResolveSimpleType(out var withNull) ?? @ref).ResolveType(items, additionalProperties);
        nullable |= withNull;
        return r;
    }

    string? ResolveSimpleType(out bool nullable)
    {
        nullable = false;
        if (type is null || type.Types.Count == 0)
            return null;

        var types = type.Types;
        if (types.Count > 1)
        {
            types = types.FindAll(t => t != "null");
            nullable = true;
        }

        if (types.Count == 1)
            return types[0];

        if (types.Count == 2 && types.Contains("string") && types.Contains("number"))
        {
            Logger.LogWarning("Type with string or number changed to string.");
            return "string";
        }

        throw new InvalidOperationException($"Not a simple type {string.Join(", ", type.Types)}");
    }

    public IEnumerable<string> GetComponents(IReadOnlyDictionary<string, SwaggerSchema> schemas, int depth)
    {
        var nullable = false;
        string? resolvedType = format == "array" || ResolveSimpleType(out nullable) == "array"
                                ? ApplyNullable(items.ResolveArrayType(additionalProperties), nullable)
                                : ResolveType();

        if (!string.IsNullOrWhiteSpace(resolvedType))
        {
            yield return resolvedType;

            if (schemas.TryGetValue(resolvedType, out var schema))
            {
                foreach (var usedType in schema.GetComponents(schemas, depth))
                {
                    yield return usedType;
                }
            }
        }
    }
}