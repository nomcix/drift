using System.Collections.Immutable;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Random;
using DirectiveDrift.Core.Serialization;

namespace DirectiveDrift.Core.Simulation;

public sealed record RunStartResult(RunState State, CanonicalEvent Event);

public static class RunStartFactory
{
    public static RunStartResult Create(
        RunId runId,
        RunDefinition definition,
        ulong seed,
        ulong stream)
    {
        Validate(definition);

        var connections = definition.Connections
            .Select(connection => new ConnectionState(
                connection.ConnectionId,
                connection.RoomA,
                connection.RoomB,
                connection.Access,
                connection.HasRadiation))
            .OrderBy(connection => connection.ConnectionId.Value, StringComparer.Ordinal)
            .ToImmutableArray();

        var agents = definition.Agents
            .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
            .Select(agent => new AgentState(
                agent.AgentId,
                agent.Archetype,
                agent.Capabilities,
                agent.MaxHealth,
                agent.MaxHealth,
                AgentStatus.Active,
                agent.StartRoomId,
                null,
                CreateModuleState(agent.Module),
                string.Empty,
                connections
                    .Where(connection =>
                        connection.RoomA == agent.StartRoomId || connection.RoomB == agent.StartRoomId)
                    .Select(connection => connection.ConnectionId)
                    .OrderBy(connectionId => connectionId.Value, StringComparer.Ordinal)
                    .ToImmutableArray(),
                []))
            .ToImmutableArray();

        var messageBonus = definition.Agents.Count(
            agent => agent.Module == SupportModule.SignalRepeater) * 2;
        var droneStart = definition.Drone.PatrolRoute[definition.Drone.InitialRouteIndex];

        var state = CanonicalStateSerializer.Normalize(
            new RunState(
                runId,
                definition.Mission,
                definition.Rules,
                0,
                RunStatus.Active,
                null,
                definition.Rooms,
                agents,
                connections,
                new GeneratorState(
                    definition.Generator.DeviceId,
                    definition.Generator.RoomId,
                    GeneratorCondition.Damaged,
                    null),
                new ConsoleState(
                    definition.ConsoleAlpha.DeviceId,
                    definition.ConsoleAlpha.RoomId,
                    definition.ConsoleAlpha.InitialCondition),
                new ConsoleState(
                    definition.ConsoleBeta.DeviceId,
                    definition.ConsoleBeta.RoomId,
                    definition.ConsoleBeta.InitialCondition),
                false,
                new RecorderState(
                    definition.Recorder.ItemId,
                    definition.Recorder.ArchiveRoomId,
                    RecorderCondition.Secured,
                    null,
                    null),
                definition.ExtractionRoomId,
                new DroneState(
                    definition.Drone.EntityId,
                    definition.Drone.PatrolRoute,
                    definition.Drone.InitialRouteIndex,
                    droneStart,
                    null,
                    0),
                new CommunicationState(
                    definition.Rules.BaseMessageBudget + messageBonus,
                    [],
                    []),
                new ScoreState(0, 0, false),
                [],
                Pcg32.Seed(seed, stream),
                1));

        var stateHash = CanonicalStateSerializer.Hash(state);
        var started = new CanonicalEvent(
            new EventId($"{runId.Value}:0"),
            0,
            0,
            TurnPhase.Start,
            CanonicalEventType.RunStarted,
            new RunStartedPayload(
                definition.Mission.MissionId,
                definition.Mission.VariantId,
                definition.Mission.RulesVersion),
            "1",
            stateHash);

        return new RunStartResult(state, started);
    }

    private static ModuleState CreateModuleState(SupportModule module)
    {
        var charges = module is SupportModule.RapidRepairKit
            or SupportModule.DecoyBeacon
            or SupportModule.HazardShield
            or SupportModule.CargoClamp
            ? 1
            : 0;

        return new ModuleState(module, charges);
    }

    private static void Validate(RunDefinition definition)
    {
        if (definition.Rules.TurnLimit <= 0
            || definition.Rules.BaseMessageBudget < 0
            || definition.Rules.MessageDelayTurns < 1
            || definition.Rules.MaxMessageLength <= 0
            || definition.Rules.BaseMemoryLength <= 0
            || definition.Rules.MemoryBufferLength < definition.Rules.BaseMemoryLength)
        {
            throw new ArgumentException("Run rules are outside their valid ranges.", nameof(definition));
        }

        if (definition.Agents.Length != 2
            || definition.Agents.Select(agent => agent.AgentId).Distinct().Count() != 2)
        {
            throw new ArgumentException("A run requires exactly two distinct agents.", nameof(definition));
        }

        var roomIds = definition.Rooms.ToHashSet();
        var referencedRooms = definition.Agents.Select(agent => agent.StartRoomId)
            .Concat(definition.Connections.SelectMany(connection =>
                new[] { connection.RoomA, connection.RoomB }))
            .Concat(
            [
                definition.Generator.RoomId,
                definition.ConsoleAlpha.RoomId,
                definition.ConsoleBeta.RoomId,
                definition.Recorder.ArchiveRoomId,
                definition.ExtractionRoomId,
            ])
            .Concat(definition.Drone.PatrolRoute);

        if (definition.Rooms.Length == 0
            || definition.Rooms.Distinct().Count() != definition.Rooms.Length
            || referencedRooms.Any(roomId => !roomIds.Contains(roomId)))
        {
            throw new ArgumentException("Run definition contains invalid room references.", nameof(definition));
        }

        if (definition.Connections.Select(connection => connection.ConnectionId).Distinct().Count()
            != definition.Connections.Length)
        {
            throw new ArgumentException("Connection IDs must be unique.", nameof(definition));
        }

        if (definition.Drone.PatrolRoute.Length == 0
            || definition.Drone.InitialRouteIndex < 0
            || definition.Drone.InitialRouteIndex >= definition.Drone.PatrolRoute.Length)
        {
            throw new ArgumentException("Drone patrol is invalid.", nameof(definition));
        }

        if (definition.Agents.Any(agent => agent.MaxHealth <= 0))
        {
            throw new ArgumentException("Agent health must be positive.", nameof(definition));
        }
    }
}
