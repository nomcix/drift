using System.Text.Json;
using System.Text.Json.Serialization;

namespace DirectiveDrift.Content.Authoring;

public sealed record MissionDocument(
    string SchemaVersion,
    string MissionId,
    string ContentVersion,
    string RulesVersion,
    string ScoreVersion,
    string Title,
    string PlayerBriefing,
    RulesDocument Rules,
    IReadOnlyList<AgentDocument> Agents,
    IReadOnlyList<RoomDocument> Rooms,
    IReadOnlyList<ConnectionDocument> Connections,
    IReadOnlyList<BriefingCardDocument> BriefingCards,
    IReadOnlyList<ModuleDocument> Modules,
    DevicesDocument Devices,
    ThreatsDocument Threats,
    IReadOnlyList<ObjectiveDocument> Objectives,
    IReadOnlyList<VariantDocument> Variants,
    ScoreDocument Score,
    PresentationDocument Presentation);

public sealed record RulesDocument(
    int TurnLimit,
    int BaseMessageBudget,
    int BriefingSlotsPerAgent,
    int MessageDelayTurns,
    int MaxMessageLength,
    int SharedDoctrineMaxLength,
    int RoleOrderMaxLength,
    int BaseMemoryMaxLength);

public sealed record AgentDocument(
    string AgentId,
    string Label,
    int Health,
    string StartRoomId,
    IReadOnlyList<string> Capabilities);

public sealed record RoomDocument(
    string RoomId,
    string Label,
    IReadOnlyList<string> Tags,
    RoomVisualDocument Visual);

public sealed record RoomVisualDocument(
    RoomShape Shape,
    PointDocument Anchor,
    SizeDocument Size,
    decimal Rotation,
    LabelPlacement LabelPlacement);

public sealed record PointDocument(decimal X, decimal Y);

public sealed record SizeDocument(decimal W, decimal H);

public sealed record ConnectionDocument(
    string ConnectionId,
    string FromRoomId,
    string ToRoomId,
    bool Bidirectional,
    ConnectionInitialState InitialState,
    bool HazardEligible,
    IReadOnlyList<PointDocument> VisualWaypoints,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? AllowedAgentIds = null);

public sealed record BriefingCardDocument(
    string CardId,
    string Title,
    string Text,
    IReadOnlyList<string> Tags,
    bool RequiredContract);

public sealed record ModuleDocument(
    string ModuleId,
    string Label,
    string Description,
    ModuleEffectType EffectType,
    IReadOnlyDictionary<string, JsonElement> Parameters);

public sealed record DevicesDocument(
    GeneratorDocument Generator,
    IReadOnlyList<ConsoleDocument> Consoles,
    GateDocument Gate,
    MissionItemDocument MissionItem);

public sealed record GeneratorDocument(
    string DeviceId,
    string RoomId,
    string RepairAgentId,
    int RepairTurns);

public sealed record ConsoleDocument(string DeviceId, string RoomId);

public sealed record GateDocument(string DeviceId, string ConnectionId);

public sealed record MissionItemDocument(string ItemId, string StartRoomId);

public sealed record ThreatsDocument(RadiationDocument Radiation, DroneDocument Drone);

public sealed record RadiationDocument(
    int Damage,
    IReadOnlyList<string> EligibleConnectionIds);

public sealed record DroneDocument(string EntityId, string StartRoomId, int Damage);

public sealed record ObjectiveDocument(
    string ObjectiveId,
    ObjectiveType Type,
    bool Required,
    IReadOnlyDictionary<string, JsonElement> Parameters);

public sealed record VariantDocument(
    string VariantId,
    string Label,
    VariantVisibility Visibility,
    IReadOnlyList<MutationDocument> Mutations);

public sealed record MutationDocument(
    MutationType Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TargetId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? RoomIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? StartIndex);

public sealed record ScoreDocument(
    int SuccessBase,
    int PerUnusedTurn,
    int PerRemainingHealth,
    int PerUnusedMessage,
    int PerUnusedModuleCharge,
    int NoFailedSyncBonus,
    int NoInterruptedRepairBonus);

public sealed record PresentationDocument(ViewBoxDocument ViewBox, string Theme);

public sealed record ViewBoxDocument(decimal Width, decimal Height);

public enum RoomShape
{
    DockingCrescent,
    TaperedTransit,
    ServiceHex,
    AngledSpine,
    RelayRing,
    RadarOctagon,
    ReactorRing,
    ConsoleWedgeUp,
    ConsoleWedgeDown,
    ArchiveIris,
    ArchiveShield,
}

public enum LabelPlacement
{
    Inside,
    Above,
    Below,
    Left,
    Right,
}

public enum ConnectionInitialState
{
    Open,
    Locked,
}

public enum ModuleEffectType
{
    RapidRepair,
    DecoyBeacon,
    MessageBudget,
    PreventHazardDamage,
    PreventCargoDrop,
    MemoryLimit,
}

public enum ObjectiveType
{
    DeviceOnline,
    SimultaneousConsoleActivation,
    ItemRecovered,
    TeamExtracted,
}

public enum VariantVisibility
{
    Practice,
    Certification,
}

public enum MutationType
{
    HazardConnection,
    DamagedDevice,
    LockedConnection,
    DronePatrol,
}
