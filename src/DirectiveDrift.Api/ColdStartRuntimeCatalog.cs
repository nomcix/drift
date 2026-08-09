using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;

namespace DirectiveDrift.Api;

public sealed class ColdStartRuntimeCatalog
{
    private ColdStartRuntimeCatalog(
        ValidatedMission mission,
        ColdStartVariantCatalog variants,
        string buildSchemaJson)
    {
        Mission = mission;
        Variants = variants;
        BuildSchemaJson = buildSchemaJson;
    }

    public ValidatedMission Mission { get; }

    public ColdStartVariantCatalog Variants { get; }

    public string BuildSchemaJson { get; }

    public static async Task<ColdStartRuntimeCatalog> LoadAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var missionJson = await File.ReadAllTextAsync(
            Path.Combine(repositoryRoot, "content/missions/cold-start/mission.json"),
            cancellationToken);
        var missionSchema = await File.ReadAllTextAsync(
            Path.Combine(repositoryRoot, "contracts/mission.schema.json"),
            cancellationToken);
        var buildSchema = await File.ReadAllTextAsync(
            Path.Combine(repositoryRoot, "contracts/build.schema.json"),
            cancellationToken);
        var certificationJson = await File.ReadAllTextAsync(
            Path.Combine(
                repositoryRoot,
                "content/missions/cold-start/server/certification-variants.json"),
            cancellationToken);
        var certificationSchema = await File.ReadAllTextAsync(
            Path.Combine(repositoryRoot, "contracts/certification-variants.schema.json"),
            cancellationToken);

        var missionResult = MissionLoader.Load(missionJson, missionSchema);
        if (!missionResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Cold Start content is invalid: {string.Join("; ", missionResult.Errors)}");
        }

        var variants = ColdStartVariantCatalogLoader.Load(
            missionResult.Mission!,
            certificationJson,
            certificationSchema);
        if (!variants.IsValid)
        {
            throw new InvalidOperationException(
                $"Cold Start variant catalogue is invalid: {string.Join("; ", variants.Errors)}");
        }

        return new ColdStartRuntimeCatalog(missionResult.Mission!, variants.Catalog!, buildSchema);
    }

    public static string FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DirectiveDrift.sln"))
                || File.Exists(
                    Path.Combine(
                        current.FullName,
                        "content/missions/cold-start/mission.json"))
                && File.Exists(Path.Combine(current.FullName, "contracts/build.schema.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the Directive Drift repository root.");
    }
}
