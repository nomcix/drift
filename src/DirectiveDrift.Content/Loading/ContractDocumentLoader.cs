using System.Text.Json;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Validation;

namespace DirectiveDrift.Content.Loading;

public static class ContractDocumentLoader
{
    public static DocumentLoadResult<MissionDocument> LoadMission(string json, string schemaJson)
        => Load<MissionDocument>(json, schemaJson);

    public static DocumentLoadResult<BuildDocument> LoadBuild(string json, string schemaJson)
        => Load<BuildDocument>(json, schemaJson);

    public static DocumentLoadResult<AgentDecisionDocument> LoadAgentDecision(
        string json,
        string schemaJson)
        => Load<AgentDecisionDocument>(json, schemaJson);

    private static DocumentLoadResult<T> Load<T>(string json, string schemaJson)
        where T : class
    {
        var schemaReport = JsonSchemaContractValidator.Validate(json, schemaJson);

        if (!schemaReport.IsValid)
        {
            return new DocumentLoadResult<T>(null, schemaReport.Errors);
        }

        try
        {
            return new DocumentLoadResult<T>(ContractJson.Deserialize<T>(json), []);
        }
        catch (JsonException)
        {
            return new DocumentLoadResult<T>(
                null,
                [
                    new ValidationError(
                        ValidationErrorCodes.ContractDeserializationFailed,
                        "/",
                        $"The document could not be read as {typeof(T).Name}."),
                ]);
        }
    }
}
