using DirectiveDrift.Content.Validation;

namespace DirectiveDrift.Content.Loading;

public static class MissionLoader
{
    public static MissionLoadResult Load(string json, string schemaJson)
    {
        var markupReport = RawPresentationMarkupGuard.Validate(json);

        if (!markupReport.IsValid)
        {
            return new MissionLoadResult(null, markupReport.Errors);
        }

        var documentResult = ContractDocumentLoader.LoadMission(json, schemaJson);

        if (!documentResult.IsValid)
        {
            return new MissionLoadResult(null, documentResult.Errors);
        }

        var referenceReport = MissionReferenceValidator.Validate(documentResult.Document!);

        return referenceReport.IsValid
            ? new MissionLoadResult(new ValidatedMission(documentResult.Document!), [])
            : new MissionLoadResult(null, referenceReport.Errors);
    }
}
