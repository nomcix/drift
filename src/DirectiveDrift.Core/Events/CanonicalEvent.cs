using System.Text.Json.Serialization;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Core.Events;

public enum TurnPhase
{
    Start,
    Deliver,
    Observe,
    Decide,
    Validate,
    Communicate,
    Move,
    Interact,
    Threat,
    Objective,
    Record,
}

public enum CanonicalEventType
{
    RunStarted,
    TurnStarted,
    MessageDelivered,
    AgentDecisionAccepted,
    AgentDecisionFallback,
    MessageQueued,
    MessageRejected,
    AgentMoved,
    RoomScanned,
    HazardSensed,
    HazardTraversed,
    RepairStarted,
    RepairContinued,
    RepairInterrupted,
    PowerRestored,
    ConsoleRepaired,
    ConsoleActivated,
    ConsoleSyncFailed,
    ArchiveOpened,
    RecorderPickedUp,
    RecorderDropped,
    DroneMoved,
    AgentDamaged,
    AgentDisabled,
    ModuleConsumed,
    ObjectiveAdvanced,
    MissionSucceeded,
    MissionFailed,
    RunSuspended,
    TurnEnded,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "payloadType")]
[JsonDerivedType(typeof(RunStartedPayload), "runStarted")]
[JsonDerivedType(typeof(TurnStartedPayload), "turnStarted")]
[JsonDerivedType(typeof(MessagePayload), "message")]
[JsonDerivedType(typeof(DecisionPayload), "decision")]
[JsonDerivedType(typeof(AgentMovedPayload), "agentMoved")]
[JsonDerivedType(typeof(RoomScannedPayload), "roomScanned")]
[JsonDerivedType(typeof(HazardPayload), "hazard")]
[JsonDerivedType(typeof(RepairPayload), "repair")]
[JsonDerivedType(typeof(PowerRestoredPayload), "powerRestored")]
[JsonDerivedType(typeof(ConsolePayload), "console")]
[JsonDerivedType(typeof(ArchiveOpenedPayload), "archiveOpened")]
[JsonDerivedType(typeof(RecorderPayload), "recorder")]
[JsonDerivedType(typeof(DroneMovedPayload), "droneMoved")]
[JsonDerivedType(typeof(AgentDamagedPayload), "agentDamaged")]
[JsonDerivedType(typeof(AgentDisabledPayload), "agentDisabled")]
[JsonDerivedType(typeof(ModuleConsumedPayload), "moduleConsumed")]
[JsonDerivedType(typeof(ObjectiveAdvancedPayload), "objectiveAdvanced")]
[JsonDerivedType(typeof(MissionTerminalPayload), "missionTerminal")]
[JsonDerivedType(typeof(TurnEndedPayload), "turnEnded")]
public interface ICanonicalEventPayload;

public sealed record RunStartedPayload(
    MissionId MissionId,
    VariantId VariantId,
    string RulesVersion) : ICanonicalEventPayload;

public sealed record TurnStartedPayload(string PreTurnStateHash) : ICanonicalEventPayload;

public sealed record MessagePayload(
    MessageId MessageId,
    AgentId SenderAgentId,
    AgentId RecipientAgentId,
    int SentTurn,
    int DeliveryTurn,
    string? Text,
    string? RejectionReason) : ICanonicalEventPayload;

public sealed record DecisionPayload(
    AgentId AgentId,
    ActionId ActionId,
    DecisionFallbackReason? FallbackReason) : ICanonicalEventPayload;

public sealed record AgentMovedPayload(
    AgentId AgentId,
    RoomId FromRoomId,
    RoomId ToRoomId,
    ConnectionId ConnectionId) : ICanonicalEventPayload;

public sealed record RoomScannedPayload(AgentId AgentId, RoomId RoomId) : ICanonicalEventPayload;

public sealed record HazardPayload(
    AgentId AgentId,
    ConnectionId ConnectionId,
    bool Prevented) : ICanonicalEventPayload;

public sealed record RepairPayload(AgentId AgentId, DeviceId DeviceId) : ICanonicalEventPayload;

public sealed record PowerRestoredPayload(DeviceId DeviceId) : ICanonicalEventPayload;

public sealed record ConsolePayload(AgentId? AgentId, DeviceId DeviceId) : ICanonicalEventPayload;

public sealed record ArchiveOpenedPayload : ICanonicalEventPayload;

public sealed record RecorderPayload(
    MissionItemId ItemId,
    AgentId? AgentId,
    RoomId RoomId) : ICanonicalEventPayload;

public sealed record DroneMovedPayload(
    EntityId EntityId,
    RoomId FromRoomId,
    RoomId ToRoomId,
    bool FollowedBeacon) : ICanonicalEventPayload;

public sealed record AgentDamagedPayload(
    AgentId AgentId,
    string Source,
    int RemainingHealth) : ICanonicalEventPayload;

public sealed record AgentDisabledPayload(AgentId AgentId) : ICanonicalEventPayload;

public sealed record ModuleConsumedPayload(
    AgentId AgentId,
    SupportModule Module) : ICanonicalEventPayload;

public sealed record ObjectiveAdvancedPayload(string Objective, string State) : ICanonicalEventPayload;

public sealed record MissionTerminalPayload(
    RunStatus Status,
    MissionFailureReason? FailureReason,
    int? Score) : ICanonicalEventPayload;

public sealed record TurnEndedPayload(string StateHash) : ICanonicalEventPayload;

public sealed record CanonicalEvent(
    EventId EventId,
    long Sequence,
    int Turn,
    TurnPhase Phase,
    CanonicalEventType Type,
    ICanonicalEventPayload Payload,
    string SchemaVersion,
    string? PostStateHash);
