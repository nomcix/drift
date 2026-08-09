using System.Text.Json;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Serialization;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Content.Tests;

public sealed class ColdStartMaterializationTests
{
    [Fact]
    public void EveryFixedMaterializationMatchesTheGoldenStateHash()
    {
        var mission = ColdStartTestContent.LoadMission();
        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(
            RepositoryFiles.Read(
                "content/missions/cold-start/golden/materialization-hashes.json"))!;
        var actual = ColdStartTestContent.LoadCatalog(mission).AllFixedVariants.ToDictionary(
            variant => variant.VariantId,
            variant =>
            {
                var result = ColdStartMissionMaterializer.Materialize(mission, variant);
                var definition = Assert.IsType<RunDefinition>(result.Definition);
                return CanonicalStateSerializer.Hash(
                    RunStartFactory.Create(new RunId("golden"), definition, 0, 1).State);
            },
            StringComparer.Ordinal);

        Assert.Equal(
            expected.OrderBy(entry => entry.Key, StringComparer.Ordinal),
            actual.OrderBy(entry => entry.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void ServerFixtureMatchesAllSixAuthoredCertificationVariants()
    {
        var catalog = ColdStartTestContent.LoadCatalog();

        Assert.Equal(5, catalog.PracticeVariants.Length);
        Assert.Equal(6, catalog.CertificationVariants.Length);
        Assert.All(
            catalog.PracticeVariants,
            variant => Assert.Equal(VariantVisibility.Practice, variant.Visibility));
        Assert.All(
            catalog.CertificationVariants,
            variant => Assert.Equal(VariantVisibility.Certification, variant.Visibility));
    }

    [Fact]
    public void AuthoredMissionMapsToAStableModuleFreeCoreDefinition()
    {
        var mission = ColdStartTestContent.LoadMission();
        var variant = ColdStartTestContent.LoadCatalog(mission).PracticeVariants[0];

        var result = ColdStartMissionMaterializer.Materialize(mission, variant);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        var definition = Assert.IsType<RunDefinition>(result.Definition);
        Assert.Equal(new VariantId("cs-practice-01"), definition.Mission.VariantId);
        Assert.All(definition.Agents, agent => Assert.Equal(SupportModule.None, agent.Module));
        Assert.Contains(
            definition.Connections,
            connection => connection.ConnectionId == new ConnectionId("service-junction")
                && connection.HasRadiation);
        Assert.Equal(
            "ef7f0854163b81e00acb2cf66b03224999706ee86908ccf0b0ca3255ed19f45b",
            CanonicalStateSerializer.Hash(
                RunStartFactory.Create(new RunId("golden"), definition, 0, 1).State));
    }

    [Fact]
    public void InvalidMutationCombinationFailsBeforeRunCreation()
    {
        var mission = ColdStartTestContent.LoadMission();
        var source = ColdStartTestContent.LoadCatalog(mission).PracticeVariants[0];
        var invalid = source with
        {
            VariantId = "invalid-double-hazard",
            Mutations = source.Mutations.Concat(
            [
                new MutationDocument(
                    MutationType.HazardConnection,
                    "west-junction",
                    null,
                    null),
            ]).ToArray(),
        };

        var result = ColdStartMissionMaterializer.Materialize(mission, invalid);

        Assert.False(result.IsValid);
        Assert.Null(result.Definition);
        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.ContentInvalidMutation);
    }

    [Fact]
    public void SeededPracticeUsesOnlyProvenCatalogueCombinations()
    {
        var mission = ColdStartTestContent.LoadMission();
        var catalog = ColdStartTestContent.LoadCatalog(mission);

        var first = ColdStartRandomMaterializer.Materialize(mission, catalog, 91);
        var second = ColdStartRandomMaterializer.Materialize(mission, catalog, 91);

        Assert.Equal(first.Variant, second.Variant);
        Assert.True(first.Result.IsValid, string.Join(Environment.NewLine, first.Result.Errors));
        Assert.StartsWith("cs-practice-random-", first.Variant.VariantId, StringComparison.Ordinal);
    }
}
