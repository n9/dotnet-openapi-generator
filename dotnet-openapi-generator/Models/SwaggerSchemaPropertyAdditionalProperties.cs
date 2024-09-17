
using System.Text.Json;
using System.Text.Json.Serialization;

namespace dotnet.openapi.generator;

internal class SwaggerSchemaPropertyAdditionalProperties
{
    public string? type { get; set; }
    public bool nullable { get; set; }
}


internal class SwaggerSchemaPropertyAdditionalPropertiesConverter : JsonConverter<SwaggerSchemaPropertyAdditionalProperties>
{
    static readonly JsonConverter<SwaggerSchemaPropertyAdditionalProperties> _default = (JsonConverter<SwaggerSchemaPropertyAdditionalProperties>)JsonSerializerOptions.Default.GetConverter(typeof(SwaggerSchemaPropertyAdditionalProperties));

    public override SwaggerSchemaPropertyAdditionalProperties? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.True => new SwaggerSchemaPropertyAdditionalProperties(),
            _ => _default.Read(ref reader, typeToConvert, options),
        };

    public override void Write(Utf8JsonWriter writer, SwaggerSchemaPropertyAdditionalProperties value, JsonSerializerOptions options)
        => _default.Write(writer, value, options);
}