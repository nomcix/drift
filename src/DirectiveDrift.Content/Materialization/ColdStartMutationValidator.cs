using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Materialization;

public static class ColdStartMutationValidator
{
    private static readonly ConnectionId SafeLockTarget = new("landing-west");

    public static ValidationReport Validate(ValidatedMission mission, VariantDocument variant)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(variant);

        var errors = new List<ValidationError>();
        var hazards = MutationsOfType(variant, MutationType.HazardConnection);
        var damaged = MutationsOfType(variant, MutationType.DamagedDevice);
        var locked = MutationsOfType(variant, MutationType.LockedConnection);
        var patrols = MutationsOfType(variant, MutationType.DronePatrol);

        RequireCount(hazards, 1, variant, "exactly one hazard mutation", errors);
        RequireMaximum(damaged, 1, variant, "at most one damaged console", errors);
        RequireMaximum(locked, 1, variant, "at most one service lock", errors);
        RequireCount(patrols, 1, variant, "exactly one drone patrol", errors);

        ValidateHazards(mission, variant, hazards, errors);
        ValidateDamagedDevices(mission, variant, damaged, errors);
        ValidateLocks(mission, variant, locked, errors);
        ValidatePatrol(mission, variant, patrols, errors);

        return new ValidationReport(
            errors
                .OrderBy(error => error.Path, StringComparer.Ordinal)
                .ThenBy(error => error.Message, StringComparer.Ordinal)
                .ToArray());
    }

    private static MutationDocument[] MutationsOfType(
        VariantDocument variant,
        MutationType type) => variant.Mutations.Where(mutation => mutation.Type == type).ToArray();

    private static void RequireCount(
        MutationDocument[] mutations,
        int expected,
        VariantDocument variant,
        string rule,
        List<ValidationError> errors)
    {
        if (mutations.Length != expected)
        {
            AddError(variant, rule, errors);
        }
    }

    private static void RequireMaximum(
        MutationDocument[] mutations,
        int maximum,
        VariantDocument variant,
        string rule,
        List<ValidationError> errors)
    {
        if (mutations.Length > maximum)
        {
            AddError(variant, rule, errors);
        }
    }

    private static void ValidateHazards(
        ValidatedMission mission,
        VariantDocument variant,
        IEnumerable<MutationDocument> hazards,
        List<ValidationError> errors)
    {
        var eligible = mission.Authoring.Threats.Radiation.EligibleConnectionIds
            .ToHashSet(StringComparer.Ordinal);

        foreach (var hazard in hazards)
        {
            if (hazard.TargetId is null || !eligible.Contains(hazard.TargetId))
            {
                AddError(variant, $"hazard target '{hazard.TargetId}' is not eligible", errors);
            }
        }
    }

    private static void ValidateDamagedDevices(
        ValidatedMission mission,
        VariantDocument variant,
        IEnumerable<MutationDocument> damaged,
        List<ValidationError> errors)
    {
        var consoleIds = mission.Authoring.Devices.Consoles
            .Select(console => console.DeviceId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var mutation in damaged)
        {
            if (mutation.TargetId is null || !consoleIds.Contains(mutation.TargetId))
            {
                AddError(variant, $"damaged target '{mutation.TargetId}' is not a console", errors);
            }
        }
    }

    private static void ValidateLocks(
        ValidatedMission mission,
        VariantDocument variant,
        IEnumerable<MutationDocument> locked,
        List<ValidationError> errors)
    {
        foreach (var mutation in locked)
        {
            if (mutation.TargetId is null
                || new ConnectionId(mutation.TargetId) != SafeLockTarget
                || !mission.Connections.ContainsKey(SafeLockTarget))
            {
                AddError(
                    variant,
                    $"lock target '{mutation.TargetId}' is outside the safe catalogue",
                    errors);
            }
        }
    }

    private static void ValidatePatrol(
        ValidatedMission mission,
        VariantDocument variant,
        MutationDocument[] patrols,
        List<ValidationError> errors)
    {
        if (patrols.Length != 1)
        {
            return;
        }

        var patrol = patrols[0];
        if (patrol.RoomIds is null
            || patrol.RoomIds.Count < 2
            || patrol.StartIndex is null
            || patrol.StartIndex < 0
            || patrol.StartIndex >= patrol.RoomIds.Count)
        {
            AddError(variant, "drone patrol shape or start index is invalid", errors);
            return;
        }

        for (var index = 0; index < patrol.RoomIds.Count; index++)
        {
            var from = new RoomId(patrol.RoomIds[index]);
            var to = new RoomId(patrol.RoomIds[(index + 1) % patrol.RoomIds.Count]);
            var connected = mission.Connections.Values.Any(connection =>
                new RoomId(connection.FromRoomId) == from && new RoomId(connection.ToRoomId) == to
                || new RoomId(connection.FromRoomId) == to && new RoomId(connection.ToRoomId) == from);

            if (!connected)
            {
                AddError(
                    variant,
                    $"drone patrol step '{from}' to '{to}' is not connected",
                    errors);
            }
        }
    }

    private static void AddError(
        VariantDocument variant,
        string rule,
        List<ValidationError> errors)
    {
        errors.Add(
            new ValidationError(
                ValidationErrorCodes.ContentInvalidMutation,
                $"/variants/{variant.VariantId}/mutations",
                $"Variant '{variant.VariantId}' violates the safe mutation catalogue: {rule}."));
    }
}
