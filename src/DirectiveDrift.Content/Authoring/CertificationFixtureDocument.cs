namespace DirectiveDrift.Content.Authoring;

public sealed record CertificationFixtureDocument(
    string SchemaVersion,
    string MissionId,
    string CertificationVersion,
    IReadOnlyList<VariantDocument> Variants);
