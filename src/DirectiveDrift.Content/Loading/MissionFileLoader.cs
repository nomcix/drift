using DirectiveDrift.Content.Validation;

namespace DirectiveDrift.Content.Loading;

public static class MissionFileLoader
{
    public static async Task<MissionLoadResult> LoadAsync(
        string missionPath,
        string schemaPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var missionJson = await File.ReadAllTextAsync(missionPath, cancellationToken);
            var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken);
            return MissionLoader.Load(missionJson, schemaJson);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new MissionLoadResult(
                null,
                [
                    new ValidationError(
                        ValidationErrorCodes.ContentFileReadFailed,
                        "/",
                        "The mission or schema file could not be read."),
                ]);
        }
    }
}
