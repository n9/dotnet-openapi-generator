using System.Text.Json;
using System.Text.Json.Serialization;

namespace dotnet.openapi.generator;

[JsonConverter(typeof(SwaggerSchemaTypeConverter))]
internal class SwaggerSchemaType(List<string> types)
{
    public readonly List<string> Types = types;
}

internal class SwaggerSchemaTypeConverter : JsonConverter<SwaggerSchemaType>
{
    public override SwaggerSchemaType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => new([JsonSerializer.Deserialize<string>(ref reader, options)!]),
            _ => new(JsonSerializer.Deserialize<List<string>>(ref reader, options)!),
        };

    public override void Write(Utf8JsonWriter writer, SwaggerSchemaType value, JsonSerializerOptions options)
    {
        if (value.Types.Count == 1)
            JsonSerializer.Serialize(writer, value.Types[0], options);
        else
            JsonSerializer.Serialize(writer, value.Types, options);
    }
}