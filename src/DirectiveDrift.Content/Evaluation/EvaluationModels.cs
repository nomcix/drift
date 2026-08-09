using System.Collections.Immutable;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Evaluation;

public enum EvaluationProviderMode
{
    Scripted,
}

public sealed record EvaluationRequest(
    EvaluationProviderMode ProviderMode,
    ImmutableArray<VariantId> VariantIds,
    int Repetitions);

public sealed record EvaluationRunResult(
    string BuildId,
    VariantId VariantId,
    int Repetition,
    RunStatus Status,
    int Turns,
    int DamageTaken,
    int FallbackDecisions,
    string? FailureSignature);

public sealed record EvaluationReport(
    string BuildId,
    EvaluationProviderMode ProviderMode,
    ImmutableArray<EvaluationRunResult> Runs)
{
    public int Successes => Runs.Count(run => run.Status == RunStatus.Succeeded);

    public int Failures => Runs.Length - Successes;

    public int TotalAgentTurns => Runs.Sum(run => run.Turns * 2);

    public int FallbackDecisions => Runs.Sum(run => run.FallbackDecisions);
}
