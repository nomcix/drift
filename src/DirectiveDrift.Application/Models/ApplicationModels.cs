using System.Collections.Immutable;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Application.Models;

public enum TurnOperationStatus
{
    Queued,
    Processing,
    Succeeded,
    Failed,
    Suspended,
}

public sealed record BuildSummary(
    string BuildId,
    string MissionId,
    string Name,
    int LatestVersion,
    DateTimeOffset CreatedAt);

public sealed record BuildVersionSnapshot(
    string BuildId,
    int Version,
    string CanonicalJson,
    bool HasBeenUsed,
    DateTimeOffset CreatedAt);

public sealed record PreparedRun(
    RunId RunId,
    string BuildId,
    int BuildVersion,
    RunState InitialState,
    CanonicalEvent InitialEvent,
    ImmutableDictionary<int, ImmutableDictionary<AgentId, ActionId>> ScriptedPlan,
    string ProviderProfileId);

public sealed record RunSummary(
    RunId RunId,
    string BuildId,
    int BuildVersion,
    MissionId MissionId,
    VariantId VariantId,
    int Turn,
    RunStatus Status,
    string StateHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TurnOperationSummary(
    string OperationId,
    RunId RunId,
    int Turn,
    string IdempotencyKey,
    TurnOperationStatus Status,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EnqueueTurnResult(
    TurnOperationSummary Operation,
    bool IsReplay,
    bool IsConflict);

public sealed record ClaimedTurnOperation(
    string OperationId,
    string LeaseToken,
    string OwnerId,
    RunId RunId,
    int Turn,
    RunState PreTurnState,
    ImmutableDictionary<AgentId, ActionId> ScriptedActions);

public sealed record ReplayData(
    RunSummary Run,
    BuildVersionSnapshot Build,
    RunState InitialState,
    ImmutableArray<CanonicalEvent> Events,
    ImmutableArray<ResolvedDecision> Decisions);

public sealed record UsageReservation(
    string ReservationId,
    string OperationId,
    int ReservedInputTokens,
    int ReservedOutputTokens,
    int ReservedCostMicros);

public sealed record UsageSettlement(
    string ReservationId,
    int InputTokens,
    int OutputTokens,
    int CostMicros);
