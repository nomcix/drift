using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;

namespace DirectiveDrift.Content.Tests;

internal static class ColdStartTestContent
{
    public static ValidatedMission LoadMission()
    {
        var result = MissionLoader.Load(
            RepositoryFiles.Read("content/missions/cold-start/mission.json"),
            RepositoryFiles.Read("contracts/mission.schema.json"));

        return Assert.IsType<ValidatedMission>(result.Mission);
    }

    public static ColdStartVariantCatalog LoadCatalog(ValidatedMission? mission = null)
    {
        var result = ColdStartVariantCatalogLoader.Load(
            mission ?? LoadMission(),
            RepositoryFiles.Read(
                "content/missions/cold-start/server/certification-variants.json"),
            RepositoryFiles.Read("contracts/certification-variants.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        return Assert.IsType<ColdStartVariantCatalog>(result.Catalog);
    }
}
