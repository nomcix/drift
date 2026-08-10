using System.Collections.Immutable;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Content.Solving;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Content.Evaluation;

public static class ScriptedEvaluationRunner
{
    public static EvaluationReport Run(
        ValidatedMission mission,
        ColdStartVariantCatalog catalog,
        BuildDocument build,
        EvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProviderMode != EvaluationProviderMode.Scripted)
        {
            throw new NotSupportedException("P3 supports scripted provider mode only.");
        }

        if (request.Repetitions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        var buildReport = BuildReferenceValidator.Validate(build, mission);
        if (!buildReport.IsValid)
        {
            throw new ArgumentException(
                string.Join(" ", buildReport.Errors.Select(error => error.Message)),
                nameof(build));
        }

        var results = ImmutableArray.CreateBuilder<EvaluationRunResult>();
        foreach (var variantId in request.VariantIds)
        {
            var variant = catalog.Find(variantId)
                ?? throw new ArgumentException($"Variant '{variantId}' is not in the selected catalogue.");
            var modules = ColdStartMissionMaterializer.MapBuildModules(mission, build);
            var materialized = ColdStartMissionMaterializer.Materialize(mission, variant, modules);
            if (!materialized.IsValid)
            {
                throw new InvalidOperationException(
                    string.Join(" ", materialized.Errors.Select(error => error.Message)));
            }

            var definition = materialized.Definition!;
            var solved = ReferenceSolver.Solve(
                definition,
                ReferencePolicyCatalog.CreateOptions(
                    definition,
                    ReferencePolicyFamily.ReconCourier));

            for (var repetition = 1; repetition <= request.Repetitions; repetition++)
            {
                results.Add(EvaluateOne(build, definition, solved, repetition));
            }
        }

        return new EvaluationReport(build.BuildId, request.ProviderMode, results.ToImmutable());
    }

    private static EvaluationRunResult EvaluateOne(
        BuildDocument build,
        RunDefinition definition,
        ReferenceSolution solved,
        int repetition)
    {
        if (!solved.Solved)
        {
            return new EvaluationRunResult(
                build.BuildId,
                definition.Mission.VariantId,
                repetition,
                RunStatus.Failed,
                0,
                0,
                0,
                "reference-unsolved");
        }

        var missingSyncAgents = build.Agents
            .Where(entry => !entry.Value.BriefingCardIds.Contains("sync-contract", StringComparer.Ordinal))
            .Select(entry => entry.Key)
            .ToHashSet();
        var scriptedTurns = ScriptedKnowledgePlan.Apply(build, definition, solved.Turns);
        var execution = Execute(definition, scriptedTurns);
        var signature = execution.State.Status == RunStatus.Succeeded
            ? null
            : missingSyncAgents.Count > 0
                && execution.Events.Any(canonicalEvent =>
                    canonicalEvent.Type == CanonicalEventType.ConsoleSyncFailed)
                    ? "unknown-required-contract"
                    : "scripted-run-failed";

        return new EvaluationRunResult(
            build.BuildId,
            definition.Mission.VariantId,
            repetition,
            execution.State.Status,
            execution.State.Turn,
            execution.State.Agents.Sum(agent => agent.MaxHealth - agent.Health),
            execution.FallbackDecisions,
            signature);
    }

    private static ScriptExecution Execute(
        RunDefinition definition,
        ImmutableArray<ScriptedTurn> turns)
    {
        var start = RunStartFactory.Create(
            new RunId($"evaluation-{definition.Mission.VariantId.Value}"),
            definition,
            0x4556414c55415445UL,
            0x5343524950544544UL);
        var state = start.State;
        var events = ImmutableArray.CreateBuilder<CanonicalEvent>();
        events.Add(start.Event);
        var fallbackDecisions = 0;

        foreach (var scriptedTurn in turns)
        {
            if (state.Status != RunStatus.Active)
            {
                break;
            }

            var proposed = scriptedTurn.Decisions.ToDictionary(
                decision => decision.AgentId,
                decision => new ProposedDecision(
                    decision.ActionId,
                    null,
                    "scripted-evaluation",
                    string.Empty));
            var result = TurnResolver.ResolveTurn(state, proposed);
            fallbackDecisions += result.Decisions.Count(decision => decision.UsedFallback);
            events.AddRange(result.Events);
            state = result.State;
        }

        return new ScriptExecution(state, events.ToImmutable(), fallbackDecisions);
    }

    private sealed record ScriptExecution(
        RunState State,
        ImmutableArray<CanonicalEvent> Events,
        int FallbackDecisions);
}
