using System.Collections.Immutable;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Core.Tests;

internal static class CoreScenario
{
    public static readonly AgentId KiteId = new("kite");
    public static readonly AgentId WrenId = new("wren");
    public static readonly RoomId ExtractionRoom = new("extraction");
    public static readonly RoomId EngineerStartRoom = new("engineer-start");
    public static readonly RoomId GeneratorRoom = new("generator-room");
    public static readonly RoomId AlphaRoom = new("alpha-room");
    public static readonly RoomId BetaRoom = new("beta-room");
    public static readonly RoomId ArchiveRoom = new("archive-room");
    public static readonly RoomId SafeDroneRoom = new("drone-safe-room");
    public static readonly RoomId CrawlRoom = new("crawl-room");
    public static readonly RoomId ServiceRoom = new("service-room");
    public static readonly DeviceId GeneratorId = new("generator");
    public static readonly DeviceId AlphaId = new("console-alpha");
    public static readonly DeviceId BetaId = new("console-beta");
    public static readonly MissionItemId RecorderId = new("flight-recorder");

    public static RunDefinition CreateDefinition(
        SupportModule kiteModule = SupportModule.None,
        SupportModule wrenModule = SupportModule.None,
        bool radiationOnStartLink = false,
        ConsoleCondition betaCondition = ConsoleCondition.Operational)
    {
        var rooms = new[]
        {
            ExtractionRoom,
            EngineerStartRoom,
            GeneratorRoom,
            AlphaRoom,
            BetaRoom,
            ArchiveRoom,
            SafeDroneRoom,
            CrawlRoom,
            ServiceRoom,
        }.ToImmutableArray();

        return new RunDefinition(
            new MissionIdentity(
                new MissionId("test-mission"),
                new VariantId("test-variant"),
                "1",
                "dd-rules-1",
                "test-score-1"),
            new RunRules(18, 6, 1, 120, 240, 400),
            rooms,
            [
                new AgentDefinition(
                    KiteId,
                    AgentArchetype.Recon,
                    AgentCapabilities.Move
                    | AgentCapabilities.Scan
                    | AgentCapabilities.SenseAdjacentRadiation
                    | AgentCapabilities.UseCrawlspace
                    | AgentCapabilities.CarryMissionItem,
                    2,
                    ExtractionRoom,
                    kiteModule),
                new AgentDefinition(
                    WrenId,
                    AgentArchetype.Engineer,
                    AgentCapabilities.Move
                    | AgentCapabilities.DiagnoseMachinery
                    | AgentCapabilities.RepairMajorSystem
                    | AgentCapabilities.RepairConsole
                    | AgentCapabilities.CarryMissionItem,
                    2,
                    EngineerStartRoom,
                    wrenModule),
            ],
            [
                new ConnectionDefinition(
                    new ConnectionId("extraction-engineer"),
                    ExtractionRoom,
                    EngineerStartRoom,
                    ConnectionAccess.Open,
                    radiationOnStartLink),
                new ConnectionDefinition(
                    new ConnectionId("engineer-generator"),
                    EngineerStartRoom,
                    GeneratorRoom,
                    ConnectionAccess.Open,
                    false),
                new ConnectionDefinition(
                    new ConnectionId("engineer-alpha"),
                    EngineerStartRoom,
                    AlphaRoom,
                    ConnectionAccess.Open,
                    false),
                new ConnectionDefinition(
                    new ConnectionId("engineer-beta"),
                    EngineerStartRoom,
                    BetaRoom,
                    ConnectionAccess.Open,
                    false),
                new ConnectionDefinition(
                    new ConnectionId("engineer-archive"),
                    EngineerStartRoom,
                    ArchiveRoom,
                    ConnectionAccess.ArchiveGate,
                    false),
                new ConnectionDefinition(
                    new ConnectionId("engineer-crawl"),
                    EngineerStartRoom,
                    CrawlRoom,
                    ConnectionAccess.ReconCrawlspace,
                    false),
                new ConnectionDefinition(
                    new ConnectionId("engineer-service"),
                    EngineerStartRoom,
                    ServiceRoom,
                    ConnectionAccess.PowerServiceLock,
                    false),
            ],
            new GeneratorDefinition(GeneratorId, GeneratorRoom),
            new ConsoleDefinition(AlphaId, AlphaRoom, ConsoleCondition.Operational),
            new ConsoleDefinition(BetaId, BetaRoom, betaCondition),
            new RecorderDefinition(RecorderId, ArchiveRoom),
            ExtractionRoom,
            new DroneDefinition(new EntityId("drone"), [SafeDroneRoom], 0));
    }

    public static RunState Start(
        SupportModule kiteModule = SupportModule.None,
        SupportModule wrenModule = SupportModule.None,
        bool radiationOnStartLink = false,
        ConsoleCondition betaCondition = ConsoleCondition.Operational) =>
        RunStartFactory.Create(
            new RunId("run-1"),
            CreateDefinition(kiteModule, wrenModule, radiationOnStartLink, betaCondition),
            42,
            54).State;

    public static ProposedDecision Decision(
        string actionId,
        string? message = null,
        string memory = "") =>
        new(new ActionId(actionId), message, "reason", memory);

    public static IReadOnlyDictionary<AgentId, ProposedDecision> Decisions(
        ProposedDecision kite,
        ProposedDecision wren,
        bool reverseInsertion = false)
    {
        var result = new Dictionary<AgentId, ProposedDecision>();
        if (reverseInsertion)
        {
            result.Add(WrenId, wren);
            result.Add(KiteId, kite);
        }
        else
        {
            result.Add(KiteId, kite);
            result.Add(WrenId, wren);
        }

        return result;
    }

    public static RunState PlaceAgent(RunState state, AgentId agentId, RoomId roomId) =>
        state with
        {
            Agents = state.Agents
                .Select(agent => agent.AgentId == agentId ? agent with { RoomId = roomId } : agent)
                .ToImmutableArray(),
        };

    public static RunState UpdateAgent(
        RunState state,
        AgentId agentId,
        Func<AgentState, AgentState> update) =>
        state with
        {
            Agents = state.Agents
                .Select(agent => agent.AgentId == agentId ? update(agent) : agent)
                .ToImmutableArray(),
        };

    public static RunState WithPowerOnline(RunState state) =>
        state with
        {
            Generator = state.Generator with
            {
                Condition = GeneratorCondition.Online,
                RepairingAgentId = null,
            },
        };

    public static RunState WithArchiveOpen(RunState state) =>
        WithPowerOnline(state) with
        {
            ArchiveGateOpen = true,
            Recorder = state.Recorder with { Condition = RecorderCondition.Available },
        };
}
