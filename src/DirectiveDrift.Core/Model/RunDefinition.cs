using System.Collections.Immutable;

namespace DirectiveDrift.Core.Model;

[Flags]
public enum AgentCapabilities
{
    None = 0,
    Move = 1 << 0,
    Scan = 1 << 1,
    SenseAdjacentRadiation = 1 << 2,
    UseCrawlspace = 1 << 3,
    CarryMissionItem = 1 << 4,
    DiagnoseMachinery = 1 << 5,
    RepairMajorSystem = 1 << 6,
    RepairConsole = 1 << 7,
}

public enum AgentArchetype
{
    Recon,
    Engineer,
}

public enum SupportModule
{
    None,
    RapidRepairKit,
    DecoyBeacon,
    SignalRepeater,
    HazardShield,
    CargoClamp,
    MemoryBuffer,
}

public enum ConnectionAccess
{
    Open,
    ReconCrawlspace,
    PowerServiceLock,
    ArchiveGate,
}

public enum ConsoleCondition
{
    Operational,
    Damaged,
}

public sealed record MissionIdentity(
    MissionId MissionId,
    VariantId VariantId,
    string ContentVersion,
    string RulesVersion,
    string ScoreVersion);

public sealed record RunRules(
    int TurnLimit,
    int BaseMessageBudget,
    int MessageDelayTurns,
    int MaxMessageLength,
    int BaseMemoryLength,
    int MemoryBufferLength);

public sealed record AgentDefinition(
    AgentId AgentId,
    AgentArchetype Archetype,
    AgentCapabilities Capabilities,
    int MaxHealth,
    RoomId StartRoomId,
    SupportModule Module);

public sealed record ConnectionDefinition(
    ConnectionId ConnectionId,
    RoomId RoomA,
    RoomId RoomB,
    ConnectionAccess Access,
    bool HasRadiation);

public sealed record GeneratorDefinition(DeviceId DeviceId, RoomId RoomId);

public sealed record ConsoleDefinition(
    DeviceId DeviceId,
    RoomId RoomId,
    ConsoleCondition InitialCondition);

public sealed record RecorderDefinition(MissionItemId ItemId, RoomId ArchiveRoomId);

public sealed record DroneDefinition(
    EntityId EntityId,
    ImmutableArray<RoomId> PatrolRoute,
    int InitialRouteIndex);

public sealed record RunDefinition(
    MissionIdentity Mission,
    RunRules Rules,
    ImmutableArray<RoomId> Rooms,
    ImmutableArray<AgentDefinition> Agents,
    ImmutableArray<ConnectionDefinition> Connections,
    GeneratorDefinition Generator,
    ConsoleDefinition ConsoleAlpha,
    ConsoleDefinition ConsoleBeta,
    RecorderDefinition Recorder,
    RoomId ExtractionRoomId,
    DroneDefinition Drone);
