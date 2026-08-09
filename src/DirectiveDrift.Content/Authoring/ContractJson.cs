using System.Text.Json;
using System.Text.Json.Serialization;

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

        return options;
    }
}
