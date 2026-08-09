using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;

namespace DirectiveDrift.Evaluation;

internal static class RepositoryContent
{
    public static ValidatedMission LoadMission()
    {
        var result = MissionLoader.Load(
            Read("content/missions/cold-start/mission.json"),
            Read("contracts/mission.schema.json"));

        return Assert.IsType<ValidatedMission>(result.Mission);
    }

    public static ColdStartVariantCatalog LoadCatalog(ValidatedMission mission)
    {
        var result = ColdStartVariantCatalogLoader.Load(
            mission,
            Read("content/missions/cold-start/server/certification-variants.json"),
            Read("contracts/certification-variants.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        return Assert.IsType<ColdStartVariantCatalog>(result.Catalog);
    }

    public static BuildDocument LoadBuild(string relativePath)
    {
        var result = ContractDocumentLoader.LoadBuild(
            Read(relativePath),
            Read("contracts/build.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        return Assert.IsType<BuildDocument>(result.Document);
    }

    private static string Read(string relativePath) => File.ReadAllText(
        Path.Combine(FindRepositoryRoot(), relativePath));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DirectiveDrift.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
