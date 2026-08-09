using System.Collections.Immutable;
using System.Text.Json;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Materialization;

public static class ColdStartMissionMaterializer
{
    public static MaterializationResult Materialize(
        ValidatedMission mission,
        VariantDocument variant,
        IReadOnlyDictionary<AgentId, SupportModule>? modulesByAgent = null)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(variant);

        var mutationReport = ColdStartMutationValidator.Validate(mission, variant);
        var boundaryErrors = ValidateMappingBoundary(mission);
        var errors = mutationReport.Errors.Concat(boundaryErrors).ToArray();

        if (errors.Length > 0)
        {
            return new MaterializationResult(null, errors);
        }

        try
        {
            return new MaterializationResult(
                CreateDefinition(mission, variant, modulesByAgent),
                []);
        }
        catch (InvalidOperationException exception)
        {
            return new MaterializationResult(
                null,
                [
                    new ValidationError(
                        ValidationErrorCodes.ContentInvariantFailed,
                        $"/variants/{variant.VariantId}",
                        exception.Message),
                ]);
        }
    }

    public static IReadOnlyDictionary<AgentId, SupportModule> MapBuildModules(
        ValidatedMission mission,
        BuildDocument build)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(build);

        return build.Agents
            .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
            .ToDictionary(
                entry => entry.Key,
                entry => MapModule(mission.Modules[new ModuleId(entry.Value.ModuleId)]));
    }

    private static RunDefinition CreateDefinition(
        ValidatedMission mission,
        VariantDocument variant,
        IReadOnlyDictionary<AgentId, SupportModule>? modulesByAgent)
    {
        var authoring = mission.Authoring;
        var hazards = Targets(variant, MutationType.HazardConnection);
        var damaged = Targets(variant, MutationType.DamagedDevice);
        var locked = Targets(variant, MutationType.LockedConnection);
        var patrol = variant.Mutations.Single(mutation => mutation.Type == MutationType.DronePatrol);
        var gateConnectionId = new ConnectionId(authoring.Devices.Gate.ConnectionId);
        var memoryBufferLength = ReadIntegerParameter(
            authoring.Modules.Single(module => module.EffectType == ModuleEffectType.MemoryLimit)
                .Parameters,
            "maxCharacters");

        var rooms = authoring.Rooms
            .Select(room => new RoomId(room.RoomId))
            .OrderBy(roomId => roomId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var agents = authoring.Agents
            .Select(agent => MapAgent(agent, modulesByAgent))
            .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var connections = authoring.Connections
            .Select(connection => new ConnectionDefinition(
                new ConnectionId(connection.ConnectionId),
                new RoomId(connection.FromRoomId),
                new RoomId(connection.ToRoomId),
                MapAccess(connection, gateConnectionId, locked),
                hazards.Contains(connection.ConnectionId)))
            .OrderBy(connection => connection.ConnectionId.Value, StringComparer.Ordinal)
            .ToImmutableArray();

        var consoleIds = ReadStringArrayParameter(
            authoring.Objectives.Single(
                objective => objective.Type == ObjectiveType.SimultaneousConsoleActivation)
                .Parameters,
            "consoleIds");
        var consoles = consoleIds
            .Select(consoleId => authoring.Devices.Consoles.Single(
                console => string.Equals(console.DeviceId, consoleId, StringComparison.Ordinal)))
            .Select(console => new ConsoleDefinition(
                new DeviceId(console.DeviceId),
                new RoomId(console.RoomId),
                damaged.Contains(console.DeviceId)
                    ? ConsoleCondition.Damaged
                    : ConsoleCondition.Operational))
            .ToArray();
        var extractionRoomId = new RoomId(
            ReadStringParameter(
                authoring.Objectives.Single(objective => objective.Type == ObjectiveType.TeamExtracted)
                    .Parameters,
                "roomId"));

        return new RunDefinition(
            new MissionIdentity(
                mission.MissionId,
                new VariantId(variant.VariantId),
                authoring.ContentVersion,
                authoring.RulesVersion,
                authoring.ScoreVersion),
            new RunRules(
                authoring.Rules.TurnLimit,
                authoring.Rules.BaseMessageBudget,
                authoring.Rules.MessageDelayTurns,
                authoring.Rules.MaxMessageLength,
                authoring.Rules.BaseMemoryMaxLength,
                memoryBufferLength),
            rooms,
            agents,
            connections,
            new GeneratorDefinition(
                new DeviceId(authoring.Devices.Generator.DeviceId),
                new RoomId(authoring.Devices.Generator.RoomId)),
            consoles[0],
            consoles[1],
            new RecorderDefinition(
                new MissionItemId(authoring.Devices.MissionItem.ItemId),
                new RoomId(authoring.Devices.MissionItem.StartRoomId)),
            extractionRoomId,
            new DroneDefinition(
                new EntityId(authoring.Threats.Drone.EntityId),
                patrol.RoomIds!.Select(roomId => new RoomId(roomId)).ToImmutableArray(),
                patrol.StartIndex!.Value));
    }

    private static AgentDefinition MapAgent(
        AgentDocument agent,
        IReadOnlyDictionary<AgentId, SupportModule>? modulesByAgent)
    {
        var agentId = new AgentId(agent.AgentId);
        var capabilities = agent.Capabilities.Aggregate(
            AgentCapabilities.None,
            (current, capability) => current | MapCapability(capability));
        var archetype = capabilities.HasFlag(AgentCapabilities.RepairMajorSystem)
            ? AgentArchetype.Engineer
            : AgentArchetype.Recon;
        var module = modulesByAgent is not null
            && modulesByAgent.TryGetValue(agentId, out var selectedModule)
                ? selectedModule
                : SupportModule.None;

        return new AgentDefinition(
            agentId,
            archetype,
            capabilities,
            agent.Health,
            new RoomId(agent.StartRoomId),
            module);
    }

    private static AgentCapabilities MapCapability(string capability) => capability switch
    {
        "move" => AgentCapabilities.Move,
        "scan" => AgentCapabilities.Scan,
        "sense-adjacent-radiation" => AgentCapabilities.SenseAdjacentRadiation,
        "use-crawlspace" => AgentCapabilities.UseCrawlspace,
        "carry-mission-item" => AgentCapabilities.CarryMissionItem,
        "diagnose-machinery" => AgentCapabilities.DiagnoseMachinery,
        "repair-major-system" => AgentCapabilities.RepairMajorSystem,
        "repair-console" => AgentCapabilities.RepairConsole,
        _ => throw new InvalidOperationException($"Capability '{capability}' cannot be materialized."),
    };

    private static SupportModule MapModule(ModuleDocument module) => module.EffectType switch
    {
        ModuleEffectType.RapidRepair => SupportModule.RapidRepairKit,
        ModuleEffectType.DecoyBeacon => SupportModule.DecoyBeacon,
        ModuleEffectType.MessageBudget => SupportModule.SignalRepeater,
        ModuleEffectType.PreventHazardDamage => SupportModule.HazardShield,
        ModuleEffectType.PreventCargoDrop => SupportModule.CargoClamp,
        ModuleEffectType.MemoryLimit => SupportModule.MemoryBuffer,
        _ => throw new InvalidOperationException(
            $"Module effect '{module.EffectType}' cannot be materialized."),
    };

    private static ConnectionAccess MapAccess(
        ConnectionDocument connection,
        ConnectionId gateConnectionId,
        HashSet<string> locked)
    {
        if (new ConnectionId(connection.ConnectionId) == gateConnectionId)
        {
            return ConnectionAccess.ArchiveGate;
        }

        if (locked.Contains(connection.ConnectionId))
        {
            return ConnectionAccess.PowerServiceLock;
        }

        return connection.AllowedAgentIds is { Count: > 0 }
            ? ConnectionAccess.ReconCrawlspace
            : ConnectionAccess.Open;
    }

    private static HashSet<string> Targets(VariantDocument variant, MutationType type) =>
        variant.Mutations
            .Where(mutation => mutation.Type == type)
            .Select(mutation => mutation.TargetId!)
            .ToHashSet(StringComparer.Ordinal);

    private static List<ValidationError> ValidateMappingBoundary(ValidatedMission mission)
    {
        var authoring = mission.Authoring;
        var errors = new List<ValidationError>();

        if (authoring.Devices.Generator.RepairTurns != 2)
        {
            AddInvariant(errors, "/devices/generator/repairTurns", "Cold Start requires two repair turns.");
        }

        if (authoring.Devices.Consoles.Count != 2)
        {
            AddInvariant(errors, "/devices/consoles", "Cold Start requires exactly two consoles.");
        }

        if (authoring.Threats.Radiation.Damage != 1 || authoring.Threats.Drone.Damage != 1)
        {
            AddInvariant(errors, "/threats", "Cold Start Core rules require one-damage threats.");
        }

        var crawlConnections = authoring.Connections
            .Where(connection => connection.AllowedAgentIds is { Count: > 0 })
            .ToArray();
        foreach (var connection in crawlConnections)
        {
            if (connection.AllowedAgentIds!.Count != 1
                || !mission.Agents.TryGetValue(
                    new AgentId(connection.AllowedAgentIds[0]),
                    out var allowedAgent)
                || !allowedAgent.Capabilities.Contains("use-crawlspace", StringComparer.Ordinal))
            {
                AddInvariant(
                    errors,
                    $"/connections/{connection.ConnectionId}/allowedAgentIds",
                    "A crawlspace must name the single crawl-capable agent.");
            }
        }

        var archetypes = authoring.Agents.Select(agent =>
            agent.Capabilities.Contains("repair-major-system", StringComparer.Ordinal)
                ? AgentArchetype.Engineer
                : AgentArchetype.Recon);
        if (archetypes.Distinct().Count() != 2)
        {
            AddInvariant(errors, "/agents", "Cold Start requires one recon and one engineer agent.");
        }

        var expectedScore = new ScoreDocument(1000, 35, 50, 20, 25, 75, 75);
        if (authoring.Score != expectedScore)
        {
            AddInvariant(
                errors,
                "/score",
                "Authored score constants do not match cold-start-score-2 Core arithmetic.");
        }

        return errors;
    }

    private static void AddInvariant(
        List<ValidationError> errors,
        string path,
        string message)
    {
        errors.Add(
            new ValidationError(
                ValidationErrorCodes.ContentInvariantFailed,
                path,
                message));
    }

    private static string ReadStringParameter(
        IReadOnlyDictionary<string, JsonElement> parameters,
        string name)
    {
        if (!parameters.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Required string parameter '{name}' is missing.");
        }

        return value.GetString()!;
    }

    private static string[] ReadStringArrayParameter(
        IReadOnlyDictionary<string, JsonElement> parameters,
        string name)
    {
        if (!parameters.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Required string-array parameter '{name}' is missing.");
        }

        return value.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static int ReadIntegerParameter(
        IReadOnlyDictionary<string, JsonElement> parameters,
        string name)
    {
        if (!parameters.TryGetValue(name, out var value) || !value.TryGetInt32(out var result))
        {
            throw new InvalidOperationException($"Required integer parameter '{name}' is missing.");
        }

        return result;
    }
}
