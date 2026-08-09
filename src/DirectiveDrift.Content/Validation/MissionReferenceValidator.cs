using System.Text.Json;
using DirectiveDrift.Content.Authoring;

namespace DirectiveDrift.Content.Validation;

public static class MissionReferenceValidator
{
    public static ValidationReport Validate(MissionDocument mission)
    {
        var errors = new List<ValidationError>();

        AddDuplicateErrors(mission.Agents, agent => agent.AgentId, "agents", "agent", errors);
        AddDuplicateErrors(mission.Rooms, room => room.RoomId, "rooms", "room", errors);
        AddDuplicateErrors(
            mission.Connections,
            connection => connection.ConnectionId,
            "connections",
            "connection",
            errors);
        AddDuplicateErrors(
            mission.BriefingCards,
            card => card.CardId,
            "briefingCards",
            "briefing card",
            errors);
        AddDuplicateErrors(mission.Modules, module => module.ModuleId, "modules", "module", errors);
        AddDuplicateErrors(
            mission.Objectives,
            objective => objective.ObjectiveId,
            "objectives",
            "objective",
            errors);
        AddDuplicateErrors(
            mission.Variants,
            variant => variant.VariantId,
            "variants",
            "variant",
            errors);

        var deviceEntries = GetDeviceEntries(mission.Devices);
        AddDuplicateErrors(deviceEntries, entry => entry.Id, "devices", "device", errors);

        var agentIds = ToIdSet(mission.Agents, agent => agent.AgentId);
        var roomIds = ToIdSet(mission.Rooms, room => room.RoomId);
        var connectionIds = ToIdSet(mission.Connections, connection => connection.ConnectionId);
        var deviceIds = ToIdSet(deviceEntries, entry => entry.Id);
        var consoleIds = ToIdSet(mission.Devices.Consoles, console => console.DeviceId);
        var eligibleHazards = ToIdSet(
            mission.Threats.Radiation.EligibleConnectionIds,
            connectionId => connectionId);

        for (var index = 0; index < mission.Agents.Count; index++)
        {
            RequireReference(
                roomIds,
                mission.Agents[index].StartRoomId,
                $"/agents/{index}/startRoomId",
                "room",
                errors);
        }

        for (var index = 0; index < mission.Connections.Count; index++)
        {
            var connection = mission.Connections[index];
            RequireReference(
                roomIds,
                connection.FromRoomId,
                $"/connections/{index}/fromRoomId",
                "room",
                errors);
            RequireReference(
                roomIds,
                connection.ToRoomId,
                $"/connections/{index}/toRoomId",
                "room",
                errors);

            if (connection.AllowedAgentIds is not null)
            {
                for (var agentIndex = 0; agentIndex < connection.AllowedAgentIds.Count; agentIndex++)
                {
                    RequireReference(
                        agentIds,
                        connection.AllowedAgentIds[agentIndex],
                        $"/connections/{index}/allowedAgentIds/{agentIndex}",
                        "agent",
                        errors);
                }
            }
        }

        RequireReference(
            roomIds,
            mission.Devices.Generator.RoomId,
            "/devices/generator/roomId",
            "room",
            errors);
        RequireReference(
            agentIds,
            mission.Devices.Generator.RepairAgentId,
            "/devices/generator/repairAgentId",
            "agent",
            errors);

        for (var index = 0; index < mission.Devices.Consoles.Count; index++)
        {
            RequireReference(
                roomIds,
                mission.Devices.Consoles[index].RoomId,
                $"/devices/consoles/{index}/roomId",
                "room",
                errors);
        }

        RequireReference(
            connectionIds,
            mission.Devices.Gate.ConnectionId,
            "/devices/gate/connectionId",
            "connection",
            errors);
        RequireReference(
            roomIds,
            mission.Devices.MissionItem.StartRoomId,
            "/devices/missionItem/startRoomId",
            "room",
            errors);
        RequireReference(
            roomIds,
            mission.Threats.Drone.StartRoomId,
            "/threats/drone/startRoomId",
            "room",
            errors);

        ValidateEligibleHazards(mission, connectionIds, errors);
        ValidateObjectives(mission, roomIds, deviceIds, consoleIds, errors);
        ValidateVariants(mission, roomIds, connectionIds, deviceIds, eligibleHazards, errors);

        return new ValidationReport(Sort(errors));
    }

    private static void ValidateEligibleHazards(
        MissionDocument mission,
        IReadOnlySet<string> connectionIds,
        List<ValidationError> errors)
    {
        var connectionsById = new Dictionary<string, ConnectionDocument>(
            StringComparer.Ordinal);

        foreach (var connection in mission.Connections)
        {
            connectionsById.TryAdd(connection.ConnectionId, connection);
        }

        for (var index = 0; index < mission.Threats.Radiation.EligibleConnectionIds.Count; index++)
        {
            var connectionId = mission.Threats.Radiation.EligibleConnectionIds[index];
            var path = $"/threats/radiation/eligibleConnectionIds/{index}";
            RequireReference(connectionIds, connectionId, path, "connection", errors);

            if (connectionsById.TryGetValue(connectionId, out var connection)
                && !connection.HazardEligible)
            {
                errors.Add(
                    new ValidationError(
                        ValidationErrorCodes.ContentInvalidReference,
                        path,
                        $"Connection '{connectionId}' is not hazard eligible."));
            }
        }
    }

    private static void ValidateObjectives(
        MissionDocument mission,
        IReadOnlySet<string> roomIds,
        IReadOnlySet<string> deviceIds,
        IReadOnlySet<string> consoleIds,
        List<ValidationError> errors)
    {
        for (var index = 0; index < mission.Objectives.Count; index++)
        {
            var objective = mission.Objectives[index];
            var path = $"/objectives/{index}/parameters";

            switch (objective.Type)
            {
                case ObjectiveType.DeviceOnline:
                    RequireParameterReference(objective.Parameters, "deviceId", deviceIds, path, "device", errors);
                    break;
                case ObjectiveType.SimultaneousConsoleActivation:
                    RequireArrayParameterReferences(
                        objective.Parameters,
                        "consoleIds",
                        consoleIds,
                        path,
                        "console",
                        errors);
                    break;
                case ObjectiveType.ItemRecovered:
                    RequireParameterValue(
                        objective.Parameters,
                        "itemId",
                        mission.Devices.MissionItem.ItemId,
                        path,
                        "mission item",
                        errors);
                    break;
                case ObjectiveType.TeamExtracted:
                    RequireParameterReference(objective.Parameters, "roomId", roomIds, path, "room", errors);
                    RequireParameterValue(
                        objective.Parameters,
                        "itemId",
                        mission.Devices.MissionItem.ItemId,
                        path,
                        "mission item",
                        errors);
                    break;
            }
        }
    }

    private static void ValidateVariants(
        MissionDocument mission,
        IReadOnlySet<string> roomIds,
        IReadOnlySet<string> connectionIds,
        IReadOnlySet<string> deviceIds,
        IReadOnlySet<string> eligibleHazards,
        List<ValidationError> errors)
    {
        for (var variantIndex = 0; variantIndex < mission.Variants.Count; variantIndex++)
        {
            var mutations = mission.Variants[variantIndex].Mutations;

            for (var mutationIndex = 0; mutationIndex < mutations.Count; mutationIndex++)
            {
                var mutation = mutations[mutationIndex];
                var path = $"/variants/{variantIndex}/mutations/{mutationIndex}";

                switch (mutation.Type)
                {
                    case MutationType.HazardConnection:
                        RequireReference(
                            eligibleHazards,
                            mutation.TargetId,
                            $"{path}/targetId",
                            "hazard-eligible connection",
                            errors);
                        break;
                    case MutationType.DamagedDevice:
                        RequireReference(
                            deviceIds,
                            mutation.TargetId,
                            $"{path}/targetId",
                            "device",
                            errors);
                        break;
                    case MutationType.LockedConnection:
                        RequireReference(
                            connectionIds,
                            mutation.TargetId,
                            $"{path}/targetId",
                            "connection",
                            errors);
                        break;
                    case MutationType.DronePatrol:
                        ValidateDronePatrol(mutation, roomIds, path, errors);
                        break;
                }
            }
        }
    }

    private static void ValidateDronePatrol(
        MutationDocument mutation,
        IReadOnlySet<string> roomIds,
        string path,
        List<ValidationError> errors)
    {
        if (mutation.RoomIds is null || mutation.StartIndex is null)
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentInvalidReference,
                    path,
                    "A drone patrol requires roomIds and startIndex."));
            return;
        }

        for (var index = 0; index < mutation.RoomIds.Count; index++)
        {
            RequireReference(
                roomIds,
                mutation.RoomIds[index],
                $"{path}/roomIds/{index}",
                "room",
                errors);
        }

        if (mutation.StartIndex.Value >= mutation.RoomIds.Count)
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentInvalidReference,
                    $"{path}/startIndex",
                    "The patrol start index must identify a patrol room."));
        }
    }

    private static void RequireParameterReference(
        IReadOnlyDictionary<string, JsonElement> parameters,
        string name,
        IReadOnlySet<string> knownIds,
        string path,
        string kind,
        List<ValidationError> errors)
    {
        if (!TryGetString(parameters, name, out var value))
        {
            AddInvalidParameter(path, name, kind, errors);
            return;
        }

        RequireReference(knownIds, value, $"{path}/{name}", kind, errors);
    }

    private static void RequireArrayParameterReferences(
        IReadOnlyDictionary<string, JsonElement> parameters,
        string name,
        IReadOnlySet<string> knownIds,
        string path,
        string kind,
        List<ValidationError> errors)
    {
        if (!parameters.TryGetValue(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            AddInvalidParameter(path, name, kind, errors);
            return;
        }

        var index = 0;

        foreach (var item in value.EnumerateArray())
        {
            RequireReference(
                knownIds,
                item.ValueKind == JsonValueKind.String ? item.GetString() : null,
                $"{path}/{name}/{index}",
                kind,
                errors);
            index++;
        }
    }

    private static void RequireParameterValue(
        IReadOnlyDictionary<string, JsonElement> parameters,
        string name,
        string expected,
        string path,
        string kind,
        List<ValidationError> errors)
    {
        if (!TryGetString(parameters, name, out var value)
            || !string.Equals(value, expected, StringComparison.Ordinal))
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentUnresolvedReference,
                    $"{path}/{name}",
                    $"The {kind} reference must resolve to '{expected}'."));
        }
    }

    private static bool TryGetString(
        IReadOnlyDictionary<string, JsonElement> parameters,
        string name,
        out string value)
    {
        if (parameters.TryGetValue(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && element.GetString() is { } stringValue)
        {
            value = stringValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static void AddInvalidParameter(
        string path,
        string name,
        string kind,
        List<ValidationError> errors)
    {
        errors.Add(
            new ValidationError(
                ValidationErrorCodes.ContentInvalidReference,
                $"{path}/{name}",
                $"A {kind} ID is required."));
    }

    private static List<(string Id, string Path)> GetDeviceEntries(DevicesDocument devices)
    {
        var entries = new List<(string Id, string Path)>
        {
            (devices.Generator.DeviceId, "/devices/generator/deviceId"),
            (devices.Gate.DeviceId, "/devices/gate/deviceId"),
        };

        entries.AddRange(
            devices.Consoles.Select(
                (console, index) => (console.DeviceId, $"/devices/consoles/{index}/deviceId")));

        return entries;
    }

    private static void AddDuplicateErrors<T>(
        IReadOnlyList<T> items,
        Func<T, string> getId,
        string pathSegment,
        string kind,
        List<ValidationError> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < items.Count; index++)
        {
            var id = getId(items[index]);

            if (!seen.Add(id))
            {
                errors.Add(
                    new ValidationError(
                        ValidationErrorCodes.ContentDuplicateId,
                        $"/{pathSegment}/{index}",
                        $"Duplicate {kind} ID '{id}'."));
            }
        }
    }

    private static HashSet<string> ToIdSet<T>(
        IEnumerable<T> items,
        Func<T, string> getId)
    {
        return items.Select(getId).ToHashSet(StringComparer.Ordinal);
    }

    private static void RequireReference(
        IReadOnlySet<string> knownIds,
        string? referencedId,
        string path,
        string kind,
        List<ValidationError> errors)
    {
        if (referencedId is null || !knownIds.Contains(referencedId))
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentUnresolvedReference,
                    path,
                    $"The {kind} ID '{referencedId ?? "(missing)"}' does not resolve."));
        }
    }

    private static ValidationError[] Sort(IEnumerable<ValidationError> errors)
    {
        return errors
            .OrderBy(error => error.Path, StringComparer.Ordinal)
            .ThenBy(error => error.Code, StringComparer.Ordinal)
            .ThenBy(error => error.Message, StringComparer.Ordinal)
            .ToArray();
    }
}
