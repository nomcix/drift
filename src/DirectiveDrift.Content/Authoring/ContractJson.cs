using System.Text.Json;
using System.Text.Json.Serialization;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Authoring;

public static class ContractJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = CreateOptions();

    public static T Deserialize<T>(string json)
        where T : class
    {
        return JsonSerializer.Deserialize<T>(json, SerializerOptions)
            ?? throw new JsonException($"The {typeof(T).Name} document was JSON null.");
    }

    public static string Serialize<T>(T document)
        where T : class
    {
        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = false,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true,
        };

        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false));
        options.Converters.Add(new AgentIdJsonConverter());

        return options;
    }

    private sealed class AgentIdJsonConverter : JsonConverter<AgentId>
    {
        public override AgentId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("Agent ID cannot be null."));

        public override void Write(
            Utf8JsonWriter writer,
            AgentId value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);

        public override AgentId ReadAsPropertyName(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("Agent ID cannot be null."));

        public override void WriteAsPropertyName(
            Utf8JsonWriter writer,
            AgentId value,
            JsonSerializerOptions options) =>
            writer.WritePropertyName(value.Value);
    }
}
