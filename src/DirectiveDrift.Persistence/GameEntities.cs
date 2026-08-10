namespace DirectiveDrift.Persistence;

public sealed class GuestProfileEntity
{
    public required string Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class BuildEntity
{
    public required string Id { get; set; }
    public required string OwnerId { get; set; }
    public required string MissionId { get; set; }
    public required string Name { get; set; }
    public int LatestVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class BuildVersionEntity
{
    public required string BuildId { get; set; }
    public int Version { get; set; }
    public required string CanonicalJson { get; set; }
    public bool HasBeenUsed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RunEntity
{
    public required string Id { get; set; }
    public required string OwnerId { get; set; }
    public required string BuildId { get; set; }
    public int BuildVersion { get; set; }
    public required string MissionId { get; set; }
    public required string VariantId { get; set; }
    public int Turn { get; set; }
    public int Status { get; set; }
    public required string StateHash { get; set; }
    public required string ProviderProfileId { get; set; }
    public required string ScriptedPlanJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RunSnapshotEntity
{
    public required string RunId { get; set; }
    public int Turn { get; set; }
    public required byte[] StateJson { get; set; }
    public required string StateHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TurnOperationEntity
{
    public required string Id { get; set; }
    public required string RunId { get; set; }
    public int Turn { get; set; }
    public required string IdempotencyKey { get; set; }
    public int Status { get; set; }
    public string? LeaseToken { get; set; }
    public long? LeaseExpiresAtUnixMilliseconds { get; set; }
    public long? HeartbeatUnixMilliseconds { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class DecisionRecordEntity
{
    public required string RunId { get; set; }
    public int Turn { get; set; }
    public required string AgentId { get; set; }
    public required string OperationId { get; set; }
    public required string ActionId { get; set; }
    public required string DecisionJson { get; set; }
}

public sealed class ProviderDecisionCheckpointEntity
{
    public required string OperationId { get; set; }
    public required string AgentId { get; set; }
    public required string ProviderProfileId { get; set; }
    public required string PreDecisionStateHash { get; set; }
    public required string ResultJson { get; set; }
    public required string ContextJson { get; set; }
    public required string PromptTemplateHash { get; set; }
    public required string DiagnosticCode { get; set; }
    public int Status { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CostMicros { get; set; }
    public int LatencyMilliseconds { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DomainEventEntity
{
    public required string RunId { get; set; }
    public long Sequence { get; set; }
    public int Turn { get; set; }
    public required string EventType { get; set; }
    public required string EventJson { get; set; }
}

public sealed class CertificationEntity
{
    public required string Id { get; set; }
    public required string OwnerId { get; set; }
    public required string Status { get; set; }
}

public sealed class CertificationRunEntity
{
    public required string CertificationId { get; set; }
    public required string RunId { get; set; }
}

public sealed class UsageLedgerEntity
{
    public required string Id { get; set; }
    public required string OwnerId { get; set; }
    public required string RunId { get; set; }
    public required string OperationId { get; set; }
    public int ReservedInputTokens { get; set; }
    public int ReservedOutputTokens { get; set; }
    public int ReservedCostMicros { get; set; }
    public int ActualInputTokens { get; set; }
    public int ActualOutputTokens { get; set; }
    public int ActualCostMicros { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SchemaMetadataEntity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
