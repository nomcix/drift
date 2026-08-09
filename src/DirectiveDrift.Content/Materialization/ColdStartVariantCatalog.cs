using System.Collections.Immutable;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Contracts;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Materialization;

public sealed record ColdStartVariantCatalog(
    string CertificationVersion,
    ImmutableArray<VariantDocument> PracticeVariants,
    ImmutableArray<VariantDocument> CertificationVariants)
{
    public ImmutableArray<VariantDocument> AllFixedVariants =>
        PracticeVariants.AddRange(CertificationVariants);

    public VariantDocument? Find(VariantId variantId) => AllFixedVariants.SingleOrDefault(
        variant => string.Equals(variant.VariantId, variantId.Value, StringComparison.Ordinal));
}

public sealed record VariantCatalogLoadResult(
    ColdStartVariantCatalog? Catalog,
    IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Catalog is not null && Errors.Count == 0;
}

public static class ColdStartVariantCatalogLoader
{
    public static VariantCatalogLoadResult Load(
        ValidatedMission mission,
        string certificationJson,
        string certificationSchemaJson)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var loadResult = ContractDocumentLoader.LoadCertificationVariants(
            certificationJson,
            certificationSchemaJson);

        if (!loadResult.IsValid)
        {
            return new VariantCatalogLoadResult(null, loadResult.Errors);
        }

        var fixture = loadResult.Document!;
        var errors = ValidateFixture(mission, fixture);
        if (errors.Length > 0)
        {
            return new VariantCatalogLoadResult(null, errors);
        }

        var practice = mission.Authoring.Variants
            .Where(variant => variant.Visibility == VariantVisibility.Practice)
            .OrderBy(variant => variant.VariantId, StringComparer.Ordinal)
            .ToImmutableArray();
        var certification = fixture.Variants
            .OrderBy(variant => variant.VariantId, StringComparer.Ordinal)
            .ToImmutableArray();

        return new VariantCatalogLoadResult(
            new ColdStartVariantCatalog(
                fixture.CertificationVersion,
                practice,
                certification),
            []);
    }

    private static ValidationError[] ValidateFixture(
        ValidatedMission mission,
        CertificationFixtureDocument fixture)
    {
        var errors = new List<ValidationError>();

        if (!string.Equals(
                fixture.SchemaVersion,
                ContractVersions.CertificationVariants,
                StringComparison.Ordinal))
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentInvalidReference,
                    "/schemaVersion",
                    $"Unsupported certification schema version '{fixture.SchemaVersion}'."));
        }

        if (!string.Equals(fixture.MissionId, mission.MissionId.Value, StringComparison.Ordinal))
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentUnresolvedReference,
                    "/missionId",
                    $"Certification mission ID '{fixture.MissionId}' does not match the mission."));
        }

        var duplicateIds = fixture.Variants
            .GroupBy(variant => variant.VariantId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal);

        foreach (var duplicateId in duplicateIds)
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentDuplicateId,
                    "/variants",
                    $"Certification variant ID '{duplicateId}' is duplicated."));
        }

        var authoredCertification = mission.Authoring.Variants
            .Where(variant => variant.Visibility == VariantVisibility.Certification)
            .ToDictionary(variant => variant.VariantId, StringComparer.Ordinal);

        for (var index = 0; index < fixture.Variants.Count; index++)
        {
            var variant = fixture.Variants[index];
            if (!authoredCertification.TryGetValue(variant.VariantId, out var authored))
            {
                errors.Add(
                    new ValidationError(
                        ValidationErrorCodes.ContentUnresolvedReference,
                        $"/variants/{index}/variantId",
                        $"Certification variant ID '{variant.VariantId}' is not authored."));
                continue;
            }

            if (!string.Equals(
                    ContractJson.Serialize(variant),
                    ContractJson.Serialize(authored),
                    StringComparison.Ordinal))
            {
                errors.Add(
                    new ValidationError(
                        ValidationErrorCodes.ContentInvalidReference,
                        $"/variants/{index}",
                        $"Certification variant '{variant.VariantId}' drifted from mission content."));
            }
        }

        foreach (var missing in authoredCertification.Keys
                     .Except(fixture.Variants.Select(variant => variant.VariantId), StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentInvalidReference,
                    "/variants",
                    $"Server fixture is missing certification variant '{missing}'."));
        }

        return errors
            .OrderBy(error => error.Path, StringComparer.Ordinal)
            .ThenBy(error => error.Message, StringComparer.Ordinal)
            .ToArray();
    }
}
