using System.Collections.Immutable;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Core.Decisions;

public static class LegalActionGenerator
{
    private const string WaitActionId = "wait";

    public static LegalActionSet GetLegalActions(RunState state, AgentId agentId)
    {
        if (state.Status != RunStatus.Active)
        {
            return new LegalActionSet(agentId, []);
        }

        var agent = state.Agents.SingleOrDefault(candidate => candidate.AgentId == agentId);
        if (agent is null || agent.Status != AgentStatus.Active)
        {
            return new LegalActionSet(agentId, []);
        }

        var actions = new List<LegalAction>();
        AddMovementActions(state, agent, actions);
        AddScanActions(state, agent, actions);
        AddGeneratorActions(state, agent, actions);
        AddConsoleActions(state, agent, actions);
        AddRecorderAction(state, agent, actions);
        AddModuleAction(agent, actions);
        actions.Add(new LegalAction(new ActionId(WaitActionId), LegalActionKind.Wait, RuleTarget.None));

        return new LegalActionSet(
            agentId,
            actions
                .OrderBy(action => action.ActionId.Value, StringComparer.Ordinal)
                .ToImmutableArray());
    }

    public static bool CanTraverse(RunState state, AgentState agent, ConnectionState connection)
    {
        return connection.Access switch
        {
            ConnectionAccess.Open => true,
            ConnectionAccess.ReconCrawlspace => agent.Capabilities.HasFlag(
                AgentCapabilities.UseCrawlspace),
            ConnectionAccess.PowerServiceLock => state.Generator.Condition == GeneratorCondition.Online,
            ConnectionAccess.ArchiveGate => state.ArchiveGateOpen,
            _ => false,
        };
    }

    private static void AddMovementActions(
        RunState state,
        AgentState agent,
        List<LegalAction> actions)
    {
        if (!agent.Capabilities.HasFlag(AgentCapabilities.Move))
        {
            return;
        }

        foreach (var connection in state.Connections
                     .Where(connection => IsIncident(connection, agent.RoomId))
                     .Where(connection => agent.DiscoveredConnections.Contains(connection.ConnectionId))
                     .Where(connection => CanTraverse(state, agent, connection)))
        {
            var destination = OtherRoom(connection, agent.RoomId);
            actions.Add(
                new LegalAction(
                    new ActionId($"move:{destination.Value}"),
                    LegalActionKind.Move,
                    new RuleTarget(RuleTargetKind.Room, destination.Value)));
        }
    }

    private static void AddScanActions(
        RunState state,
        AgentState agent,
        List<LegalAction> actions)
    {
        if (!agent.Capabilities.HasFlag(AgentCapabilities.Scan))
        {
            return;
        }

        foreach (var roomId in state.Connections
                     .Where(connection => IsIncident(connection, agent.RoomId))
                     .Select(connection => OtherRoom(connection, agent.RoomId))
                     .Where(roomId => !agent.ScannedRooms.Contains(roomId))
                     .Distinct()
                     .OrderBy(roomId => roomId.Value, StringComparer.Ordinal))
        {
            actions.Add(
                new LegalAction(
                    new ActionId($"scan:{roomId.Value}"),
                    LegalActionKind.Scan,
                    new RuleTarget(RuleTargetKind.Room, roomId.Value)));
        }
    }

    private static void AddGeneratorActions(
        RunState state,
        AgentState agent,
        List<LegalAction> actions)
    {
        if (!agent.Capabilities.HasFlag(AgentCapabilities.RepairMajorSystem)
            || agent.RoomId != state.Generator.RoomId)
        {
            return;
        }

        if (state.Generator.Condition == GeneratorCondition.Damaged)
        {
            actions.Add(
                new LegalAction(
                    new ActionId($"repair:{state.Generator.DeviceId.Value}"),
                    LegalActionKind.RepairGenerator,
                    new RuleTarget(RuleTargetKind.Device, state.Generator.DeviceId.Value)));
        }
        else if (state.Generator.Condition == GeneratorCondition.Repairing
                 && state.Generator.RepairingAgentId == agent.AgentId)
        {
            actions.Add(
                new LegalAction(
                    new ActionId($"continue-repair:{state.Generator.DeviceId.Value}"),
                    LegalActionKind.ContinueGeneratorRepair,
                    new RuleTarget(RuleTargetKind.Device, state.Generator.DeviceId.Value)));
        }
    }

    private static void AddConsoleActions(
        RunState state,
        AgentState agent,
        List<LegalAction> actions)
    {
        foreach (var console in new[] { state.ConsoleAlpha, state.ConsoleBeta })
        {
            if (agent.RoomId != console.RoomId)
            {
                continue;
            }

            if (console.Condition == ConsoleCondition.Damaged
                && agent.Capabilities.HasFlag(AgentCapabilities.RepairConsole))
            {
                actions.Add(
                    new LegalAction(
                        new ActionId($"repair:{console.DeviceId.Value}"),
                        LegalActionKind.RepairConsole,
                        new RuleTarget(RuleTargetKind.Device, console.DeviceId.Value)));
            }
            else if (console.Condition == ConsoleCondition.Operational
                     && state.Generator.Condition == GeneratorCondition.Online
                     && !state.ArchiveGateOpen)
            {
                actions.Add(
                    new LegalAction(
                        new ActionId($"activate:{console.DeviceId.Value}"),
                        LegalActionKind.ActivateConsole,
                        new RuleTarget(RuleTargetKind.Device, console.DeviceId.Value)));
            }
        }
    }

    private static void AddRecorderAction(
        RunState state,
        AgentState agent,
        List<LegalAction> actions)
    {
        if (!agent.Capabilities.HasFlag(AgentCapabilities.CarryMissionItem)
            || agent.CarriedItemId is not null)
        {
            return;
        }

        var recorderCanBePickedUp =
            state.Recorder.Condition == RecorderCondition.Available
            && agent.RoomId == state.Recorder.ArchiveRoomId
            || state.Recorder.Condition == RecorderCondition.Dropped
            && agent.RoomId == state.Recorder.DroppedRoomId;

        if (recorderCanBePickedUp)
        {
            actions.Add(
                new LegalAction(
                    new ActionId($"pickup:{state.Recorder.ItemId.Value}"),
                    LegalActionKind.PickupRecorder,
                    new RuleTarget(RuleTargetKind.MissionItem, state.Recorder.ItemId.Value)));
        }
    }

    private static void AddModuleAction(AgentState agent, List<LegalAction> actions)
    {
        if (agent.Module.Module == SupportModule.DecoyBeacon
            && agent.Module.ChargesRemaining > 0)
        {
            actions.Add(
                new LegalAction(
                    new ActionId("deploy:decoy-beacon"),
                    LegalActionKind.DeployDecoyBeacon,
                    RuleTarget.None));
        }
    }

    internal static bool IsIncident(ConnectionState connection, RoomId roomId) =>
        connection.RoomA == roomId || connection.RoomB == roomId;

    internal static RoomId OtherRoom(ConnectionState connection, RoomId roomId) =>
        connection.RoomA == roomId ? connection.RoomB : connection.RoomA;
}
