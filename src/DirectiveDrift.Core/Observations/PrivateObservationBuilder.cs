using System.Collections.Immutable;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Serialization;

namespace DirectiveDrift.Core.Observations;

public enum HazardObservation
{
    Unknown,
    Clear,
    Radiation,
}

public enum ObservedEntityKind
{
    Agent,
    Generator,
    Console,
    Recorder,
    Drone,
}

public sealed record ObservedExit(
    ConnectionId ConnectionId,
    RoomId DestinationRoomId,
    HazardObservation Hazard);

public sealed record ObservedEntity(
    ObservedEntityKind Kind,
    string EntityId,
    string? DiagnosedState);

public sealed record SelfObservation(
    int Health,
    AgentStatus Status,
    RoomId RoomId,
    MissionItemId? CarriedItemId,
    ModuleState Module,
    string Memory,
    bool HasGeneratorCommitment);

public sealed record PrivateObservation(
    AgentId AgentId,
    int Turn,
    string PreDecisionStateHash,
    SelfObservation Self,
    ImmutableArray<ObservedExit> Exits,
    ImmutableArray<ObservedEntity> LocalEntities,
    ImmutableArray<AgentMessage> DeliveredMessages,
    ImmutableArray<RoomId> ScannedRooms,
    ImmutableArray<PublicFact> PublicFacts,
    LegalActionSet LegalActions);

public static class PrivateObservationBuilder
{
    public static PrivateObservation Build(RunState state, AgentId agentId)
    {
        var agent = state.Agents.Single(candidate => candidate.AgentId == agentId);
        var canSenseRadiation = agent.Capabilities.HasFlag(
            AgentCapabilities.SenseAdjacentRadiation);

        var exits = state.Connections
            .Where(connection => LegalActionGenerator.IsIncident(connection, agent.RoomId))
            .Where(connection => agent.DiscoveredConnections.Contains(connection.ConnectionId))
            .Where(connection => LegalActionGenerator.CanTraverse(state, agent, connection))
            .OrderBy(connection => connection.ConnectionId.Value, StringComparer.Ordinal)
            .Select(connection => new ObservedExit(
                connection.ConnectionId,
                LegalActionGenerator.OtherRoom(connection, agent.RoomId),
                canSenseRadiation
                    ? connection.HasRadiation
                        ? HazardObservation.Radiation
                        : HazardObservation.Clear
                    : HazardObservation.Unknown))
            .ToImmutableArray();

        return new PrivateObservation(
            agentId,
            state.Turn + 1,
            CanonicalStateSerializer.Hash(state),
            new SelfObservation(
                agent.Health,
                agent.Status,
                agent.RoomId,
                agent.CarriedItemId,
                agent.Module,
                agent.Memory,
                state.Generator.Condition == GeneratorCondition.Repairing
                && state.Generator.RepairingAgentId == agentId),
            exits,
            GetLocalEntities(state, agent),
            state.Communication.DeliveredMessages
                .Where(message => message.RecipientAgentId == agentId)
                .OrderBy(message => message.DeliveryTurn)
                .ThenBy(message => message.MessageId.Value, StringComparer.Ordinal)
                .ToImmutableArray(),
            agent.ScannedRooms.OrderBy(room => room.Value, StringComparer.Ordinal).ToImmutableArray(),
            state.PublicFacts.Order().ToImmutableArray(),
            LegalActionGenerator.GetLegalActions(state, agentId));
    }

    private static ImmutableArray<ObservedEntity> GetLocalEntities(
        RunState state,
        AgentState observer)
    {
        var entities = new List<ObservedEntity>();

        entities.AddRange(
            state.Agents
                .Where(agent => agent.AgentId != observer.AgentId && agent.RoomId == observer.RoomId)
                .Select(agent => new ObservedEntity(
                    ObservedEntityKind.Agent,
                    agent.AgentId.Value,
                    agent.Status.ToString())));

        var diagnosesMachinery = observer.Capabilities.HasFlag(
            AgentCapabilities.DiagnoseMachinery);

        if (state.Generator.RoomId == observer.RoomId)
        {
            entities.Add(
                new ObservedEntity(
                    ObservedEntityKind.Generator,
                    state.Generator.DeviceId.Value,
                    diagnosesMachinery ? state.Generator.Condition.ToString() : null));
        }

        foreach (var console in new[] { state.ConsoleAlpha, state.ConsoleBeta })
        {
            if (console.RoomId == observer.RoomId)
            {
                entities.Add(
                    new ObservedEntity(
                        ObservedEntityKind.Console,
                        console.DeviceId.Value,
                        diagnosesMachinery ? console.Condition.ToString() : null));
            }
        }

        if (RecorderIsInRoom(state.Recorder, observer.RoomId))
        {
            entities.Add(
                new ObservedEntity(
                    ObservedEntityKind.Recorder,
                    state.Recorder.ItemId.Value,
                    state.Recorder.Condition.ToString()));
        }

        if (state.Drone.CurrentRoomId == observer.RoomId)
        {
            entities.Add(
                new ObservedEntity(
                    ObservedEntityKind.Drone,
                    state.Drone.EntityId.Value,
                    null));
        }

        return entities
            .OrderBy(entity => entity.Kind)
            .ThenBy(entity => entity.EntityId, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool RecorderIsInRoom(RecorderState recorder, RoomId roomId) =>
        recorder.Condition == RecorderCondition.Available && recorder.ArchiveRoomId == roomId
        || recorder.Condition == RecorderCondition.Dropped && recorder.DroppedRoomId == roomId;
}
