using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PluginRegistration.Core.EarlyBound;

/// <summary>
/// Accepts JSON string ("account"), pipe-separated string ("account|contact"), or string array.
/// </summary>
internal sealed class FlexibleStringListJsonConverter : JsonConverter<List<string>?>
{
    public override List<string>? ReadJson(
        JsonReader reader,
        Type objectType,
        List<string>? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonToken.String)
        {
            var value = reader.Value?.ToString();
            return ParseDelimited(value);
        }

        if (reader.TokenType == JsonToken.StartArray)
        {
            var array = JArray.Load(reader);
            return array
                .Select(token => token.Type == JTokenType.String ? token.Value<string>() : token.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();
        }

        throw new JsonSerializationException(
            $"Expected string or array for entity list, but got {reader.TokenType}.");
    }

    public override void WriteJson(JsonWriter writer, List<string>? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            writer.WriteValue(item);
        }

        writer.WriteEndArray();
    }

    private static List<string>? ParseDelimited(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }
}