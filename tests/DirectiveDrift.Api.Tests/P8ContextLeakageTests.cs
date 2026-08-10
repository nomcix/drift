using DirectiveDrift.AI;
using DirectiveDrift.Api;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Api.Tests;

public sealed class P8ContextLeakageTests
{
    [Fact]
    public async Task ActualProviderRequestSerializerExcludesForbiddenMissionAndPartnerMarkers()
    {
        var root = FindRepositoryRoot();
        var catalog = await ColdStartRuntimeCatalog.LoadAsync(root, default);
        var sourceBuild = ContractJson.Deserialize<BuildDocument>(
            await File.ReadAllTextAsync(Path.Combine(root, "examples/generic-optimal-build.json")));
        var selfId = sourceBuild.Agents.Keys.OrderBy(value => value.Value, StringComparer.Ordinal).First();
        var partnerId = sourceBuild.Agents.Keys.Single(value => value != selfId);
        var selfBuild = sourceBuild.Agents[selfId];
        var forbiddenCardId = catalog.Mission.BriefingCards.Keys
            .Select(value => value.Value)
            .First(value => !selfBuild.BriefingCardIds.Contains(value, StringComparer.Ordinal));
        var markedCards = catalog.Mission.Authoring.BriefingCards.Select(
            card => string.Equals(card.CardId, forbiddenCardId, StringComparison.Ordinal)
                ? card with { Text = "forbidden-unassigned-card-marker" }
                : card).ToArray();
        var markedMission = new ValidatedMission(
            catalog.Mission.Authoring with
            {
                PlayerBriefing = "forbidden-complete-mission-marker",
                BriefingCards = markedCards,
            });
        var markedAgents = sourceBuild.Agents.ToDictionary(value => value.Key, value => value.Value);
        markedAgents[partnerId] = markedAgents[partnerId] with
        {
            RoleOrder = "forbidden-partner-role-marker",
        };
        var build = sourceBuild with
        {
            SharedDoctrine = "allowed-shared-doctrine-marker",
            Agents = markedAgents,
        };
        var variant = catalog.Variants.PracticeVariants[0];
        var materialized = ColdStartMissionMaterializer.Materialize(
            markedMission,
            variant,
            ColdStartMissionMaterializer.MapBuildModules(markedMission, build));
        Assert.True(materialized.IsValid);
        var start = RunStartFactory.Create(
            new RunId("run-leakage-test"),
            materialized.Definition!,
            123,
            456);
        var context = new AgentTurnContextFactory(markedMission).Create(
            start.State,
            ContractJson.Serialize(build),
            selfId,
            ProviderProfiles.OpenAi);
        var prompt = PromptAssembler.Assemble(context, ProviderProfiles.OpenAi);
        var serializedRequest = OpenAiResponsesTransport.CreateRequestJson(
            new ProviderTransportRequest(ProviderProfiles.OpenAi, prompt, null));

        Assert.Contains("allowed-shared-doctrine-marker", serializedRequest, StringComparison.Ordinal);
        Assert.Contains(selfBuild.RoleOrder, serializedRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("forbidden-complete-mission-marker", serializedRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("forbidden-unassigned-card-marker", serializedRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("forbidden-partner-role-marker", serializedRequest, StringComparison.Ordinal);
        Assert.DoesNotContain(variant.VariantId, serializedRequest, StringComparison.Ordinal);
        Assert.DoesNotContain(partnerId.Value, serializedRequest, StringComparison.Ordinal);
    }

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

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
