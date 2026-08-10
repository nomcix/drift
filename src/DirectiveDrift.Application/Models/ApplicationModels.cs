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
    string ProviderProfileId,
    RunKind Kind = RunKind.Practice,
    string? CertificationId = null,
    string? VariantDisclosureJson = null);

public enum RunKind
{
    Practice,
    Certification,
}

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
    DateTimeOffset UpdatedAt,
    string ProviderProfileId,
    RunKind Kind,
    bool Assisted,
    string? CertificationId,
    string? VariantDisclosureJson);

public sealed record CertificationRunSummary(
    int Slot,
    RunId RunId,
    RunStatus Status,
    bool? Succeeded,
    string? VariantDisclosureJson);

public sealed record CertificationSummary(
    string CertificationId,
    string BuildId,
    int BuildVersion,
    string ProviderProfileId,
    string MissionContentVersion,
    string RulesVersion,
    string ScoreVersion,
    string CertificationVersion,
    string Status,
    int Successes,
    bool Revealed,
    ImmutableArray<string> Badges,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    ImmutableArray<CertificationRunSummary> Runs);

public sealed record DecisionDifference(
    int Turn,
    AgentId AgentId,
    string? LeftActionId,
    string? RightActionId);

public sealed record BuildDifference(
    bool SharedDoctrineChanged,
    ImmutableArray<AgentId> RoleOrdersChanged,
    ImmutableArray<AgentId> BriefingLoadoutsChanged,
    ImmutableArray<AgentId> ModulesChanged);

public sealed record RunComparison(
    RunSummary Left,
    RunSummary Right,
    BuildDifference Build,
    DecisionDifference? FirstDifferingDecision,
    int LeftScore,
    int RightScore,
    int LeftCostMicros,
    int RightCostMicros);

public sealed record PlayerUsageAllowance(
    int DailyLimitMicros,
    int UsedMicros,
    int RemainingMicros,
    int ScriptedRunsRemaining);

public sealed record InternalRunDiagnostics(
    RunId RunId,
    int InputTokens,
    int OutputTokens,
    int CostMicros,
    int ProviderAttempts);

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
    ImmutableDictionary<AgentId, ActionId> ScriptedActions,
    string CanonicalBuildJson,
    string ProviderProfileId);

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

public enum ProviderMode
{
    Scripted,
    Fake,
    Live,
}

public sealed record ProviderProfile(
    string ProfileId,
    ProviderMode Mode,
    string Model,
    string PromptTemplateVersion,
    int MaximumInputTokens,
    int MaximumOutputTokens,
    int MaximumResponseBytes,
    TimeSpan AttemptTimeout,
    int MaximumRepairRetries,
    int InputPriceMicrosPerMillionTokens,
    int OutputPriceMicrosPerMillionTokens,
    int TurnOperationCostCapMicros,
    int RunCostCapMicros,
    int GuestDailyCostCapMicros,
    int DeploymentDailyCostCapMicros,
    int ConcurrencyCap,
    string PriceTableVersion,
    int RunAttemptCap = 40);

public sealed record AgentIdentityView(
    AgentId AgentId,
    string Label);

public sealed record BriefingCardView(
    string CardId,
    string Title,
    string Text);

public sealed record AgentCapabilityView(
    IReadOnlyList<string> Capabilities);

public sealed record ModuleView(
    string ModuleId,
    string Label,
    string Description);

public sealed record DeliveredMessageView(
    string MessageId,
    AgentId FromAgentId,
    int SentTurn,
    int DeliveryTurn,
    string Text);

public sealed record LegalActionView(
    ActionId ActionId,
    string Kind,
    string? TargetId);

public sealed record RuntimeLimits(
    int MessageCharacters,
    int MemoryCharacters,
    int RationaleCharacters,
    int OutputTokens,
    int ResponseBytes);

public sealed record AgentTurnContext(
    string ContextVersion,
    RunId RunId,
    int Turn,
    AgentIdentityView Self,
    string UniversalRules,
    string SharedDoctrine,
    string RoleOrder,
    IReadOnlyList<BriefingCardView> BriefingCards,
    AgentCapabilityView Capabilities,
    ModuleView? Module,
    DirectiveDrift.Core.Observations.PrivateObservation Observation,
    IReadOnlyList<DeliveredMessageView> DeliveredMessages,
    string PrivateMemory,
    IReadOnlyList<LegalActionView> LegalActions,
    RuntimeLimits Limits);

public enum ProviderAttemptStatus
{
    Accepted,
    TransportError,
    Timeout,
    ResponseTooLarge,
    MalformedJson,
    InvalidSchema,
    InvalidDecision,
    Fallback,
}

public sealed record ProviderUsage(
    int InputTokens,
    int OutputTokens,
    int CostMicros,
    bool IsEstimated);

public sealed record ProviderAttemptDiagnostic(
    int Attempt,
    ProviderAttemptStatus Status,
    string DiagnosticCode,
    int InputTokens,
    int OutputTokens,
    int LatencyMilliseconds,
    string? ProviderRequestId);

public sealed record ProviderDecisionResult(
    ProposedDecision Decision,
    ProviderAttemptStatus Status,
    ProviderUsage Usage,
    int LatencyMilliseconds,
    string? ProviderRequestId,
    string PriceTableVersion,
    string PromptTemplateHash,
    string ContextHash,
    string DiagnosticCode,
    bool RepairAttempted,
    int AttemptCount,
    string ContextJson,
    IReadOnlyList<ProviderAttemptDiagnostic>? AttemptDiagnostics = null);
