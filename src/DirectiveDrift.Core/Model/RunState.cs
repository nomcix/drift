using System.Collections.Immutable;
using DirectiveDrift.Core.Random;

namespace DirectiveDrift.Core.Model;

public enum RunStatus
{
    Active,
    Succeeded,
    Failed,
    Suspended,
}

public enum MissionFailureReason
{
    Deadline,
    AgentDisabled,
    RecorderIrrecoverable,
    ContentInvariant,
    RunBudgetExhausted,
}

public enum AgentStatus
{
    Active,
    Disabled,
}

public enum GeneratorCondition
{
    Damaged,
    Repairing,
    Online,
}

public enum RecorderCondition
{
    Secured,
    Available,
    Carried,
    Dropped,
    Extracted,
}

public enum PublicFact
{
    PowerRestored,
    ArchiveOpened,
    AgentDisabled,
}

public sealed record ModuleState(SupportModule Module, int ChargesRemaining);

public sealed record AgentState(
    AgentId AgentId,
    AgentArchetype Archetype,
    AgentCapabilities Capabilities,
    int MaxHealth,
    int Health,
    AgentStatus Status,
    RoomId RoomId,
    MissionItemId? CarriedItemId,
    ModuleState Module,
    string Memory,
    ImmutableArray<ConnectionId> DiscoveredConnections,
    ImmutableArray<RoomId> ScannedRooms);

public sealed record ConnectionState(
    ConnectionId ConnectionId,
    RoomId RoomA,
    RoomId RoomB,
    ConnectionAccess Access,
    bool HasRadiation);

public sealed record GeneratorState(
    DeviceId DeviceId,
    RoomId RoomId,
    GeneratorCondition Condition,
    AgentId? RepairingAgentId);

public sealed record ConsoleState(
    DeviceId DeviceId,
    RoomId RoomId,
    ConsoleCondition Condition);

public sealed record RecorderState(
    MissionItemId ItemId,
    RoomId ArchiveRoomId,
    RecorderCondition Condition,
    AgentId? CarrierAgentId,
    RoomId? DroppedRoomId);

public sealed record DroneState(
    EntityId EntityId,
    ImmutableArray<RoomId> PatrolRoute,
    int PatrolIndex,
    RoomId CurrentRoomId,
    RoomId? BeaconRoomId,
    int BeaconStepsRemaining);

public sealed record AgentMessage(
    MessageId MessageId,
    AgentId SenderAgentId,
    AgentId RecipientAgentId,
    int SentTurn,
    int DeliveryTurn,
    string Text);

public sealed record CommunicationState(
    int RemainingMessages,
    ImmutableArray<AgentMessage> QueuedMessages,
    ImmutableArray<AgentMessage> DeliveredMessages);

public sealed record ScoreState(
    int FailedConsoleActivations,
    int InterruptedMajorRepairs,
    bool Assisted);

public sealed record RunState(
    RunId RunId,
    MissionIdentity Mission,
    RunRules Rules,
    int Turn,
    RunStatus Status,
    MissionFailureReason? FailureReason,
    ImmutableArray<RoomId> Rooms,
    ImmutableArray<AgentState> Agents,
    ImmutableArray<ConnectionState> Connections,
    GeneratorState Generator,
    ConsoleState ConsoleAlpha,
    ConsoleState ConsoleBeta,
    bool ArchiveGateOpen,
    RecorderState Recorder,
    RoomId ExtractionRoomId,
    DroneState Drone,
    CommunicationState Communication,
    ScoreState Score,
    ImmutableArray<PublicFact> PublicFacts,
    Pcg32State Random,
    long NextEventSequence);
