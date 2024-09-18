﻿using System.Text.Json.Serialization;

namespace dotnet.openapi.generator;

internal class SwaggerSchema
{
    public SwaggerSchemaEnum? @enum { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("flagged-enum")]
    public SwaggerSchemaFlaggedEnum? flaggedEnum { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("x-enumNames")]
    public List<string>? enumNames { get; set; }

    public SwaggerSchemaProperties? properties { get; set; }

    public SwaggerAllOfs? allOf { get; set; }
    public SwaggerSchemaDiscriminator? discriminator { get; set; }

    public IEnumerable<(string Key, SwaggerSchemaProperty Value)>? IterateProperties() => properties?.Iterate(discriminator?.propertyName);

    private string GetDefinitionType(string name, IEnumerable<SwaggerSchema> schemas)
    {
        if (@enum is not null)
        {
            return "enum";
        }

        if (IsTopLevel(name, schemas))
        {
            return "sealed class";
        }

        return "class";
    }

    private bool IsTopLevel(string name, IEnumerable<SwaggerSchema> schemas)
    {
        foreach (var schema in schemas)
        {
            if (schema.allOf is not null)
            {
                foreach (var allOf in schema.allOf)
                {
                    if (allOf.ResolveType() == name)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    public string? GetCtor(string name, string? jsonConstructorAttribute, bool supportRequiredProperties, IReadOnlyDictionary<string, SwaggerSchema> schemas)
    {
        if (properties is null)
        {
            return null;
        }

        HashSet<(string key, SwaggerSchemaProperty value)> baseProperties = [];
        HashSet<(string key, SwaggerSchemaProperty value)> requiredProperties = GetRequiredProperties();

        if (allOf is not null)
        {
            foreach (var inherited in allOf)
            {
                var type = inherited.ResolveType();
                if (!string.IsNullOrEmpty(type) && schemas.TryGetValue(type, out var parentSchema))
                {
                    foreach (var property in parentSchema.GetRequiredProperties())
                    {
                        baseProperties.Add(property);
                    }
                }
            }
        }

        if (baseProperties.Count == 0 && requiredProperties.Count == 0)
        {
            return null;
        }

        var parameters = baseProperties.Union(requiredProperties).Select(x => x.value.ResolveType() + " " + x.key.AsSafeVariableName());
        var assignements = requiredProperties.Select(x =>
        {
            var assignee = x.key[0..1].ToUpperInvariant() + x.key[1..];
            var assignment = x.key.AsSafeVariableName();

            if (char.IsDigit(assignee[0]))
            {
                assignee = "_" + assignee;
            }

            if (assignee == assignment)
            {
                assignee = "this." + assignee;
            }

            return assignee + " = " + assignment + ";";
        });

        var requiredCtorAttributes = "";
        var shouldOmitDefaultCtor = string.IsNullOrWhiteSpace(jsonConstructorAttribute);

        jsonConstructorAttribute = "[" + jsonConstructorAttribute + "] ";

        if (supportRequiredProperties)
        {
            requiredCtorAttributes = "[System.Diagnostics.CodeAnalysis.SetsRequiredMembers] " + jsonConstructorAttribute;
            jsonConstructorAttribute = "";
            shouldOmitDefaultCtor = true;
        }

        return $@"
    {(shouldOmitDefaultCtor ? "" : jsonConstructorAttribute)}public {name}() {{ }}

    {requiredCtorAttributes}public {name}({string.Join(", ", parameters)}){(baseProperties.Count == 0 ? "" : " : base(" + string.Join(", ", baseProperties.Select(x => x.key)) + ")")}
    {{
{string.Join(Environment.NewLine, assignements.Select(x => "        " + x))}
    }}
";
    }

    private HashSet<(string key, SwaggerSchemaProperty value)> GetRequiredProperties() => IterateProperties()?.Where(x => !x.Value.nullable /*|| x.Value.required.GetValueOrDefault()*/).ToHashSet() ?? new(0);

    private string GetInheritance()
    {
        HashSet<string> toImplement = [];

        if (allOf is not null)
        {
            foreach (var item in allOf.Select(x => x.ResolveType()))
            {
                if (item is not null)
                {
                    toImplement.Add(item);
                }
            }
        }

        if (properties is not null)
        {
            toImplement.Add("__ICanIterate");
        }

        if (toImplement.Count > 0)
        {
            return " : " + string.Join(", ", toImplement);
        }

        return "";
    }

    public string? GetBody(string name, bool supportRequiredProperties, string? jsonPropertyNameAttribute, SwaggerComponentSchemas schemas, string modifier)
    {
        return @enum?.GetBody(name, flaggedEnum, enumNames, modifier) ?? properties?.GetBody(allOf, supportRequiredProperties, jsonPropertyNameAttribute, schemas, discriminator?.propertyName);
    }

    public Task Generate(string path, string @namespace, string modifier, string name, string? jsonConstructorAttribute, string? jsonPolymorphicAttribute, string? jsonDerivedTypeAttribute, string? jsonPropertyNameAttribute, bool supportRequiredProperties, SwaggerComponentSchemas schemas, CancellationToken token)
    {
        name = name.AsSafeString();
        var fileName = Path.Combine(path, name + ".cs");

        string attributes = "";
        if (jsonPolymorphicAttribute is not null && jsonDerivedTypeAttribute is not null)
        {
            if (properties is not null && discriminator is not null && properties.TryGetValue(discriminator.propertyName, out SwaggerSchemaProperty? discriminatorProperty))
            {
                string discriminatorAttributes = $"[{jsonPolymorphicAttribute.Replace("{name}", discriminator.propertyName)}]{Environment.NewLine}" +
                                                    string.Join(Environment.NewLine, discriminator.mapping
                                                                                                  .Select(x => new
                                                                                                  {
                                                                                                      TypeName = x.Value.ResolveType(),
                                                                                                      DiscriminatorValue = x.Key
                                                                                                  })
                                                                                                  .Where(x => x.TypeName is not null && schemas.ContainsKey(x.TypeName))
                                                                                                  .Select(x => $"[{jsonDerivedTypeAttribute.Replace("{type}", x.TypeName).Replace("{value}", x.DiscriminatorValue)}]"));

                attributes += Environment.NewLine + discriminatorAttributes;
            }
        }

        var template = Constants.Header + $@"namespace {@namespace}.Models;

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]{attributes}
{(@enum is null
    ? "[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]" + Environment.NewLine
    : (flaggedEnum is not null
        ? "[System.Flags]" + Environment.NewLine
        : "")
        + "[System.Text.Json.Serialization.JsonConverter(typeof("+name+@"EnumConverter))]
")}{modifier} {GetDefinitionType(name, schemas.Values)} {name}{GetInheritance()}
{{{GetCtor(name, jsonConstructorAttribute, supportRequiredProperties, schemas)}
{GetBody(name, supportRequiredProperties, jsonPropertyNameAttribute, schemas, modifier)}
}}
";
        return Lib.EnsureTextWritten(fileName, template, token);
    }

    public IEnumerable<string> GetComponents(IReadOnlyDictionary<string, SwaggerSchema> schemas, int depth)
    {
        if (++depth < 1000)
        {
            if (properties is not null)
            {
                foreach (var property in IterateProperties()!)
                {
                    foreach (var component in property.Value.GetComponents(schemas, depth))
                    {
                        yield return component;
                    }
                }
            }

            if (allOf is not null)
            {
                foreach (var item in allOf)
                {
                    foreach (var component in item.GetComponents(schemas, depth))
                    {
                        yield return component;
                    }
                }
            }
        }
    }
}