using System.Collections.Immutable;
using System.Text.Json;
using DirectiveDrift.AI;
using DirectiveDrift.Api;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Evaluation;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Content.Solving;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.EvaluationCli;

public sealed record LiveEvaluationRun(
    string BuildId,
    string VariantId,
    int Repetition,
    string Status,
    int Turns,
    int DamageTaken,
    int AgentTurns,
    int InvalidDecisions,
    int RepairAttempts,
    int ProviderAttempts,
    int InputTokens,
    int OutputTokens,
    int CostMicros,
    string? FailureSignature,
    ImmutableArray<string> DecisionTrace,
    ImmutableDictionary<string, int> Diagnostics);

public sealed record LiveEvaluationReport(
    string SchemaVersion,
    string BuildId,
    string ProviderProfileId,
    string Model,
    string PriceTableVersion,
    int SpendCapMicros,
    ImmutableArray<LiveEvaluationRun> Runs)
{
    public int Successes => Runs.Count(run => run.Status == "succeeded");

    public int AgentTurns => Runs.Sum(run => run.AgentTurns);

    public int InvalidDecisions => Runs.Sum(run => run.InvalidDecisions);

    public int CostMicros => Runs.Sum(run => run.CostMicros);
}

public sealed record P10GateResult(
    bool Passed,
    int GenericSuccesses,
    int DesignedSuccesses,
    double InvalidDecisionRate,
    ImmutableArray<string> Failures);

public static class P10GateEvaluator
{
    public static P10GateResult Evaluate(
        LiveEvaluationReport generic,
        LiveEvaluationReport designed)
    {
        ArgumentNullException.ThrowIfNull(generic);
        ArgumentNullException.ThrowIfNull(designed);
        var failures = ImmutableArray.CreateBuilder<string>();
        ValidateMatrix(generic, "generic", failures);
        ValidateMatrix(designed, "designed", failures);
        if (generic.ProviderProfileId != designed.ProviderProfileId
            || generic.Model != designed.Model
            || generic.PriceTableVersion != designed.PriceTableVersion)
        {
            failures.Add("provider-profile-mismatch");
        }

        var gap = designed.Successes - generic.Successes;
        if (generic.Successes > 10)
        {
            failures.Add("generic-above-25-percent");
        }
        if (designed.Successes < 28)
        {
            failures.Add("designed-below-70-percent");
        }
        if (gap < 18)
        {
            failures.Add("designed-gap-below-18");
        }

        var turns = generic.AgentTurns + designed.AgentTurns;
        var invalid = generic.InvalidDecisions + designed.InvalidDecisions;
        var invalidRate = turns == 0 ? 1 : (double)invalid / turns;
        if (invalidRate >= 0.02)
        {
            failures.Add("invalid-decision-rate-not-below-2-percent");
        }

        if (gap > 0)
        {
            var largestVariantGap = designed.Runs.GroupBy(run => run.VariantId)
                .Select(group => group.Count(run => run.Status == "succeeded")
                    - generic.Runs.Count(run => run.VariantId == group.Key && run.Status == "succeeded"))
                .DefaultIfEmpty()
                .Max();
            if (largestVariantGap * 2 > gap)
            {
                failures.Add("single-variant-dominates-gap");
            }
        }

        return new P10GateResult(
            failures.Count == 0,
            generic.Successes,
            designed.Successes,
            invalidRate,
            failures.ToImmutable());
    }

    private static void ValidateMatrix(
        LiveEvaluationReport report,
        string label,
        ImmutableArray<string>.Builder failures)
    {
        if (report.Runs.Length != 40
            || report.Runs.Select(run => run.VariantId).Distinct(StringComparer.Ordinal).Count() != 8
            || report.Runs.GroupBy(run => run.VariantId).Any(group => group.Count() != 5))
        {
            failures.Add($"{label}-matrix-not-8x5");
        }
    }
}

public sealed class EvaluationCircuitBreaker
{
    private int settledMicros;
    private int reservedMicros;

    public EvaluationCircuitBreaker(int spendCapMicros, int initialSettledMicros = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spendCapMicros);
        ArgumentOutOfRangeException.ThrowIfNegative(initialSettledMicros);
        if (initialSettledMicros > spendCapMicros)
        {
            throw new EvaluationBudgetExceededException();
        }

        SpendCapMicros = spendCapMicros;
        settledMicros = initialSettledMicros;
    }

    public int SpendCapMicros { get; }

    public Reservation Reserve(ProviderProfile profile, int agents, int runSettledMicros)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(agents);

        var attempts = checked(profile.MaximumRepairRetries + 1);
        var input = Cost(profile.MaximumInputTokens, profile.InputPriceMicrosPerMillionTokens);
        var output = Cost(profile.MaximumOutputTokens, profile.OutputPriceMicrosPerMillionTokens);
        var maximum = checked((input + output) * attempts * agents);
        if (maximum > profile.TurnOperationCostCapMicros
            || checked(runSettledMicros + maximum) > profile.RunCostCapMicros
            || checked(settledMicros + reservedMicros + maximum) > SpendCapMicros)
        {
            throw new EvaluationBudgetExceededException();
        }

        reservedMicros = checked(reservedMicros + maximum);
        return new Reservation(this, maximum);
    }

    private static int Cost(int tokens, int priceMicrosPerMillionTokens) =>
        checked((int)(((long)tokens * priceMicrosPerMillionTokens + 999_999) / 1_000_000));

    public sealed class Reservation(EvaluationCircuitBreaker owner, int maximumMicros)
    {
        private bool settled;

        public void Settle(int actualMicros)
        {
            if (settled || actualMicros < 0 || actualMicros > maximumMicros)
            {
                throw new InvalidOperationException("Evaluation reservation settlement is invalid.");
            }

            owner.reservedMicros = checked(owner.reservedMicros - maximumMicros);
            owner.settledMicros = checked(owner.settledMicros + actualMicros);
            settled = true;
        }
    }
}

public sealed class EvaluationBudgetExceededException()
    : Exception("evaluation-spend-cap");

public static class LiveEvaluationRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task<LiveEvaluationReport> RunAsync(
        ValidatedMission mission,
        ColdStartVariantCatalog catalog,
        BuildDocument build,
        ImmutableArray<VariantId> variants,
        int repetitions,
        IAgentDecisionProvider provider,
        int spendCapMicros,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (provider.Profile.Mode != ProviderMode.Live || repetitions <= 0)
        {
            throw new ArgumentException("Live evaluation requires a live provider and positive repetitions.");
        }

        var existing = await ReadExistingAsync(outputPath, build, provider, spendCapMicros, cancellationToken);
        var completed = existing.Runs.ToDictionary(
            run => (run.VariantId, run.Repetition),
            run => run);
        var breaker = new EvaluationCircuitBreaker(spendCapMicros, existing.CostMicros);

        var buildJson = ContractJson.Serialize(build);
        var contextFactory = new AgentTurnContextFactory(mission);
        foreach (var variantId in variants)
        {
            var variant = catalog.Find(variantId)
                ?? throw new ArgumentException($"Variant '{variantId}' is not in the catalogue.");
            var modules = ColdStartMissionMaterializer.MapBuildModules(mission, build);
            var materialized = ColdStartMissionMaterializer.Materialize(mission, variant, modules);
            if (!materialized.IsValid)
            {
                throw new InvalidOperationException(string.Join(" ", materialized.Errors.Select(error => error.Message)));
            }

            var definition = materialized.Definition!;
            var solution = ReferenceSolver.Solve(
                definition,
                ReferencePolicyCatalog.CreateOptions(definition, ReferencePolicyFamily.ReconCourier));
            if (!solution.Solved)
            {
                throw new InvalidOperationException(solution.Failure ?? "Evaluation variant is not solvable.");
            }

            var scripted = ScriptedKnowledgePlan.Apply(build, definition, solution.Turns)
                .ToImmutableDictionary(turn => turn.Turn);
            for (var repetition = 1; repetition <= repetitions; repetition++)
            {
                if (completed.ContainsKey((variantId.Value, repetition)))
                {
                    continue;
                }

                var run = await EvaluateOneAsync(
                    build,
                    buildJson,
                    definition,
                    scripted,
                    repetition,
                    provider,
                    contextFactory,
                    breaker,
                    cancellationToken);
                completed.Add((variantId.Value, repetition), run);
                var report = existing with
                {
                    Runs = completed.Values
                        .OrderBy(value => value.VariantId, StringComparer.Ordinal)
                        .ThenBy(value => value.Repetition)
                        .ToImmutableArray(),
                };
                await WriteAtomicAsync(outputPath, report, cancellationToken);
                Console.WriteLine(
                    $"variant={run.VariantId} repetition={run.Repetition} status={run.Status} "
                    + $"turns={run.Turns} invalid={run.InvalidDecisions}/{run.AgentTurns} costMicros={run.CostMicros}");
            }
        }

        return existing with
        {
            Runs = completed.Values
                .OrderBy(value => value.VariantId, StringComparer.Ordinal)
                .ThenBy(value => value.Repetition)
                .ToImmutableArray(),
        };
    }

    private static async Task<LiveEvaluationRun> EvaluateOneAsync(
        BuildDocument build,
        string buildJson,
        RunDefinition definition,
        ImmutableDictionary<int, ScriptedTurn> scripted,
        int repetition,
        IAgentDecisionProvider provider,
        AgentTurnContextFactory contextFactory,
        EvaluationCircuitBreaker breaker,
        CancellationToken cancellationToken)
    {
        var start = RunStartFactory.Create(
            new RunId($"eval-{build.BuildId}-{definition.Mission.VariantId.Value}-{repetition}"),
            definition,
            ReferenceSolver.ScriptedSeed,
            ReferenceSolver.ScriptedStream);
        var state = start.State;
        var agentTurns = 0;
        var invalid = 0;
        var repairAttempts = 0;
        var attempts = 0;
        var input = 0;
        var output = 0;
        var cost = 0;
        var diagnostics = new Dictionary<string, int>(StringComparer.Ordinal);
        var trace = ImmutableArray.CreateBuilder<string>();

        while (state.Status == RunStatus.Active)
        {
            var active = state.Agents
                .Where(agent => agent.Status == AgentStatus.Active)
                .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
                .ToArray();
            var reservation = breaker.Reserve(provider.Profile, active.Length, cost);
            var turn = scripted[state.Turn + 1];
            var requests = active.Select(agent =>
            {
                var other = active.Single(candidate => candidate.AgentId != agent.AgentId);
                return new AgentDecisionRequest(
                    $"eval-{build.BuildId}-{definition.Mission.VariantId.Value}-{repetition}-{state.Turn + 1}",
                    state.Turn + 1,
                    agent.AgentId,
                    LegalActionGenerator.GetLegalActions(state, agent.AgentId),
                    turn.Decisions.Single(decision => decision.AgentId == agent.AgentId).ActionId,
                    agent.Memory,
                    other.AgentId,
                    contextFactory.Create(state, buildJson, agent.AgentId, provider.Profile));
            }).ToArray();
            var results = await Task.WhenAll(requests.Select(request => provider.DecideAsync(request, cancellationToken)));
            var turnCost = results.Sum(result => result.Usage.CostMicros);
            reservation.Settle(turnCost);
            cost = checked(cost + turnCost);
            agentTurns += results.Length;
            invalid += results.Count(result => result.Decision.ForcedFallbackReason is not null);
            repairAttempts += results.Count(result => result.RepairAttempted);
            attempts += results.Sum(result => result.AttemptCount);
            input = checked(input + results.Sum(result => result.Usage.InputTokens));
            output = checked(output + results.Sum(result => result.Usage.OutputTokens));
            foreach (var result in results)
            {
                diagnostics[result.DiagnosticCode] = diagnostics.GetValueOrDefault(result.DiagnosticCode) + 1;
            }
            var proposed = requests.Select((request, index) => (request.AgentId, results[index].Decision))
                .ToDictionary(value => value.AgentId, value => value.Decision);
            foreach (var decision in proposed.OrderBy(value => value.Key.Value, StringComparer.Ordinal))
            {
                var sent = decision.Value.Message is null ? string.Empty : ":message";
                trace.Add($"{state.Turn + 1}:{decision.Key.Value}:{decision.Value.ActionId.Value}{sent}");
            }
            state = TurnResolver.ResolveTurn(state, proposed).State;
        }

        return new LiveEvaluationRun(
            build.BuildId,
            definition.Mission.VariantId.Value,
            repetition,
            state.Status.ToString().ToLowerInvariant(),
            state.Turn,
            state.Agents.Sum(agent => agent.MaxHealth - agent.Health),
            agentTurns,
            invalid,
            repairAttempts,
            attempts,
            input,
            output,
            cost,
            FailureSignature(state),
            trace.ToImmutable(),
            diagnostics.ToImmutableDictionary(StringComparer.Ordinal));
    }

    private static string? FailureSignature(RunState state)
    {
        if (state.Status == RunStatus.Succeeded)
        {
            return null;
        }

        if (state.FailureReason != MissionFailureReason.Deadline)
        {
            return state.FailureReason?.ToString().ToLowerInvariant() ?? "terminal-failure";
        }

        if (state.Generator.Condition != GeneratorCondition.Online)
        {
            return "power-incomplete";
        }
        if (!state.ArchiveGateOpen)
        {
            return "sync-incomplete";
        }
        if (state.Recorder.Condition != RecorderCondition.Extracted)
        {
            return "recorder-incomplete";
        }

        return "extraction-incomplete";
    }

    private static async Task<LiveEvaluationReport> ReadExistingAsync(
        string outputPath,
        BuildDocument build,
        IAgentDecisionProvider provider,
        int spendCapMicros,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(outputPath))
        {
            return new LiveEvaluationReport(
                "p10-live-evaluation-v1",
                build.BuildId,
                provider.ProfileId,
                provider.Profile.Model,
                provider.Profile.PriceTableVersion,
                spendCapMicros,
                []);
        }

        var report = JsonSerializer.Deserialize<LiveEvaluationReport>(
            await File.ReadAllTextAsync(outputPath, cancellationToken),
            Json) ?? throw new InvalidOperationException("Evaluation report is invalid.");
        if (report.SchemaVersion != "p10-live-evaluation-v1"
            || report.BuildId != build.BuildId
            || report.ProviderProfileId != provider.ProfileId
            || report.SpendCapMicros != spendCapMicros)
        {
            throw new InvalidOperationException("Evaluation resume metadata does not match this run.");
        }

        return report;
    }

    private static async Task WriteAtomicAsync(
        string outputPath,
        LiveEvaluationReport report,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = $"{fullPath}.tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(report, Json), cancellationToken);
        File.Move(temporary, fullPath, true);
    }
}
