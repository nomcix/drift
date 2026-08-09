using System.Collections.Immutable;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Content.Solving;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Tests;

public sealed class ReferenceSolverTests
{
    [Fact]
    public void PracticeTwoCargoClampGoldenRouteReplaysWithinSeventeenTurns()
    {
        var mission = ColdStartTestContent.LoadMission();
        var variant = ColdStartTestContent.LoadCatalog(mission).PracticeVariants.Single(
            candidate => candidate.VariantId == "cs-practice-02");
        var baseResult = ColdStartMissionMaterializer.Materialize(mission, variant);
        var baseDefinition = Assert.IsType<RunDefinition>(baseResult.Definition);
        var result = ColdStartMissionMaterializer.Materialize(
            mission,
            variant,
            ReferencePolicyCatalog.CreateModules(
                baseDefinition,
                ReferencePolicyFamily.ReconCourier));
        var definition = Assert.IsType<RunDefinition>(result.Definition);
        var recon = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Recon);
        var engineer = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Engineer);

        var replay = ReferenceSolver.Replay(
            definition,
            new[]
            {
                Turn(1, recon.AgentId, "move:maintenance-alcove", engineer.AgentId, "move:east-hall"),
                Turn(2, recon.AgentId, "move:east-hall", engineer.AgentId, "move:junction"),
                Turn(3, recon.AgentId, "wait", engineer.AgentId, "move:auxiliary-power"),
                Turn(4, recon.AgentId, "move:junction", engineer.AgentId, "repair:auxiliary-generator"),
                Turn(5, recon.AgentId, "move:console-alpha", engineer.AgentId, "move:junction"),
                Turn(6, recon.AgentId, "wait", engineer.AgentId, "move:console-beta"),
                Turn(7, recon.AgentId, "wait", engineer.AgentId, "repair:console-beta"),
                Turn(8, recon.AgentId, "activate:console-alpha", engineer.AgentId, "activate:console-beta"),
                Turn(9, recon.AgentId, "move:junction", engineer.AgentId, "wait"),
                Turn(10, recon.AgentId, "move:archive-threshold", engineer.AgentId, "move:junction"),
                Turn(11, recon.AgentId, "move:archive", engineer.AgentId, "move:east-hall"),
                Turn(12, recon.AgentId, "pickup:flight-recorder", engineer.AgentId, "move:maintenance-alcove"),
                Turn(13, recon.AgentId, "move:archive-threshold", engineer.AgentId, "move:landing-bay"),
                Turn(14, recon.AgentId, "move:junction", engineer.AgentId, "wait"),
                Turn(15, recon.AgentId, "move:east-hall", engineer.AgentId, "wait"),
                Turn(16, recon.AgentId, "move:maintenance-alcove", engineer.AgentId, "wait"),
                Turn(17, recon.AgentId, "move:landing-bay", engineer.AgentId, "wait"),
            });

        Assert.True(replay.Solved, replay.Failure);
    }

    [Fact]
    public void EveryFixedVariantHasInterchangeableAndNoDamageProofsWithinSeventeenTurns()
    {
        var mission = ColdStartTestContent.LoadMission();
        var catalog = ColdStartTestContent.LoadCatalog(mission);

        var validation = ColdStartContentValidator.Validate(mission, catalog);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Equal(11, validation.Proofs.Length);
        Assert.All(
            validation.Proofs,
            proof =>
            {
                Assert.InRange(proof.AlternateLoadout.CompletionTurn!.Value, 1, 17);
                Assert.InRange(proof.NoDamage.CompletionTurn!.Value, 1, 17);
                Assert.Equal(0, proof.NoDamage.DamageTaken);
                Assert.Equal(2, proof.ConsoleRoleAssignments);
                Assert.True(proof.SafeSyncWindows >= 2);

                var alternateModules = proof.AlternateLoadout.FinalState!.Agents
                    .Select(agent => agent.Module.Module)
                    .ToHashSet();
                var noDamageModules = proof.NoDamage.FinalState!.Agents
                    .Select(agent => agent.Module.Module)
                    .ToHashSet();
                Assert.Empty(alternateModules.Intersect(noDamageModules));
            });
    }

    [Fact]
    public void EachNamedReferenceFamilyPassesItsApplicableVariants()
    {
        var mission = ColdStartTestContent.LoadMission();
        var catalog = ColdStartTestContent.LoadCatalog(mission);

        foreach (var family in ReferencePolicyCatalog.Families)
        {
            Assert.True(family.ApplicableVariantIds.Length >= 2);

            foreach (var variantId in family.ApplicableVariantIds)
            {
                var variant = Assert.IsType<VariantDocument>(catalog.Find(variantId));
                var baseResult = ColdStartMissionMaterializer.Materialize(mission, variant);
                var baseDefinition = Assert.IsType<RunDefinition>(baseResult.Definition);
                var modules = ReferencePolicyCatalog.CreateModules(baseDefinition, family.Family);
                var materialized = ColdStartMissionMaterializer.Materialize(
                    mission,
                    variant,
                    modules);
                var definition = Assert.IsType<RunDefinition>(materialized.Definition);
                var solution = ReferenceSolver.Solve(
                    definition,
                    ReferencePolicyCatalog.CreateOptions(definition, family.Family));

                Assert.True(
                    solution.Solved,
                    $"{family.Name} / {variantId}: {solution.Failure}");
            }
        }
    }

    [Fact]
    public void PracticeOneHasModuleFreeAndNoDamageProofsWithinSeventeenTurns()
    {
        var mission = ColdStartTestContent.LoadMission();
        var variant = ColdStartTestContent.LoadCatalog(mission).PracticeVariants.Single(
            candidate => candidate.VariantId == "cs-practice-01");
        var moduleFree = ColdStartMissionMaterializer.Materialize(mission, variant);
        var definition = Assert.IsType<RunDefinition>(moduleFree.Definition);
        var recon = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Recon);
        var engineer = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Engineer);

        var manual = ReferenceSolver.Replay(
            definition,
            new[]
            {
                Turn(1, recon.AgentId, "move:west-hall", engineer.AgentId, "move:east-hall"),
                Turn(2, recon.AgentId, "move:junction", engineer.AgentId, "move:junction"),
                Turn(3, recon.AgentId, "move:console-alpha", engineer.AgentId, "move:auxiliary-power"),
                Turn(4, recon.AgentId, "wait", engineer.AgentId, "repair:auxiliary-generator"),
                Turn(5, recon.AgentId, "wait", engineer.AgentId, "continue-repair:auxiliary-generator"),
                Turn(6, recon.AgentId, "move:junction", engineer.AgentId, "move:junction"),
                Turn(7, recon.AgentId, "move:console-alpha", engineer.AgentId, "move:console-beta"),
                Turn(8, recon.AgentId, "activate:console-alpha", engineer.AgentId, "activate:console-beta"),
                Turn(9, recon.AgentId, "move:junction", engineer.AgentId, "wait"),
                Turn(10, recon.AgentId, "move:archive-threshold", engineer.AgentId, "move:junction"),
                Turn(11, recon.AgentId, "move:archive", engineer.AgentId, "move:west-hall"),
                Turn(12, recon.AgentId, "pickup:flight-recorder", engineer.AgentId, "move:landing-bay"),
                Turn(13, recon.AgentId, "move:archive-threshold", engineer.AgentId, "wait"),
                Turn(14, recon.AgentId, "move:junction", engineer.AgentId, "wait"),
                Turn(15, recon.AgentId, "move:west-hall", engineer.AgentId, "wait"),
                Turn(16, recon.AgentId, "move:landing-bay", engineer.AgentId, "wait"),
            });
        Assert.True(manual.Solved, manual.Failure);

        var moduleFreeSolution = ReferenceSolver.Solve(
            definition,
            new ReferencePolicyOptions(
                recon.AgentId,
                recon.AgentId,
                RequireNoDamage: false));

        Assert.True(moduleFreeSolution.Solved, moduleFreeSolution.Failure);
        Assert.InRange(moduleFreeSolution.CompletionTurn!.Value, 1, 17);
        Assert.All(
            moduleFreeSolution.FinalState!.Agents,
            agent => Assert.Equal(SupportModule.None, agent.Module.Module));

        var noDamageResult = ColdStartMissionMaterializer.Materialize(
            mission,
            variant,
            ReferencePolicyCatalog.CreateModules(definition, ReferencePolicyFamily.BeaconWindow));
        var noDamageDefinition = Assert.IsType<RunDefinition>(noDamageResult.Definition);
        var noDamageSolution = ReferenceSolver.Solve(
            noDamageDefinition,
            ReferencePolicyCatalog.CreateOptions(
                noDamageDefinition,
                ReferencePolicyFamily.BeaconWindow));

        Assert.True(noDamageSolution.Solved, noDamageSolution.Failure);
        Assert.InRange(noDamageSolution.CompletionTurn!.Value, 1, 17);
        Assert.Equal(0, noDamageSolution.DamageTaken);
    }

    private static ScriptedTurn Turn(
        int turn,
        AgentId firstAgentId,
        string firstActionId,
        AgentId secondAgentId,
        string secondActionId) =>
        new(
            turn,
            ImmutableArray.Create(
                new ScriptedAgentDecision(firstAgentId, new ActionId(firstActionId)),
                new ScriptedAgentDecision(secondAgentId, new ActionId(secondActionId))));
}
