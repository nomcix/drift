using System.Collections.Immutable;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Content.Solving;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Validation;

public sealed record VariantProof(
    VariantId VariantId,
    ReferenceSolution AlternateLoadout,
    ReferenceSolution NoDamage,
    int ConsoleRoleAssignments,
    int SafeSyncWindows);

public sealed record ColdStartValidationResult(
    ImmutableArray<VariantProof> Proofs,
    ImmutableArray<ValidationError> Errors)
{
    public bool IsValid => Errors.Length == 0
        && Proofs.Length > 0
        && Proofs.All(proof => proof.AlternateLoadout.Solved && proof.NoDamage.Solved);
}

public static class ColdStartContentValidator
{
    public static ColdStartValidationResult Validate(
        ValidatedMission mission,
        ColdStartVariantCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(catalog);

        var errors = new List<ValidationError>();
        var proofs = ImmutableArray.CreateBuilder<VariantProof>();
        ValidateMissionWideInvariants(mission, errors);

        foreach (var variant in catalog.AllFixedVariants)
        {
            var mutationReport = ColdStartMutationValidator.Validate(mission, variant);
            errors.AddRange(mutationReport.Errors);

            var baseResult = ColdStartMissionMaterializer.Materialize(mission, variant);
            if (!baseResult.IsValid)
            {
                errors.AddRange(baseResult.Errors);
                continue;
            }

            var baseDefinition = baseResult.Definition!;
            var roleAssignments = CountConsoleRoleAssignments(baseDefinition);
            var safeWindows = CountSafeSyncWindows(baseDefinition);
            ValidateTopology(baseDefinition, variant, roleAssignments, safeWindows, errors);

            var alternate = SolveFamily(
                mission,
                variant,
                baseDefinition,
                ReferencePolicyFamily.ReconCourier);
            var noDamage = SolveFamily(
                mission,
                variant,
                baseDefinition,
                ReferencePolicyFamily.BeaconWindow);
            AddSolverError(variant, "alternate-loadout", alternate, errors);
            AddSolverError(variant, "no-damage", noDamage, errors);

            proofs.Add(
                new VariantProof(
                    new VariantId(variant.VariantId),
                    alternate,
                    noDamage,
                    roleAssignments,
                    safeWindows));
        }

        return new ColdStartValidationResult(
            proofs.ToImmutable(),
            errors
                .OrderBy(error => error.Path, StringComparer.Ordinal)
                .ThenBy(error => error.Message, StringComparer.Ordinal)
                .ToImmutableArray());
    }

    private static ReferenceSolution SolveFamily(
        ValidatedMission mission,
        VariantDocument variant,
        RunDefinition baseDefinition,
        ReferencePolicyFamily family)
    {
        var modules = ReferencePolicyCatalog.CreateModules(baseDefinition, family);
        var materialized = ColdStartMissionMaterializer.Materialize(mission, variant, modules);
        if (!materialized.IsValid)
        {
            return new ReferenceSolution(
                false,
                null,
                [],
                [],
                0,
                string.Join(" ", materialized.Errors.Select(error => error.Message)));
        }

        return ReferenceSolver.Solve(
            materialized.Definition!,
            ReferencePolicyCatalog.CreateOptions(materialized.Definition!, family));
    }

    private static void ValidateMissionWideInvariants(
        ValidatedMission mission,
        List<ValidationError> errors)
    {
        if (mission.Authoring.BriefingCards.Count != 10
            || mission.Authoring.Rules.BriefingSlotsPerAgent != 4)
        {
            AddInvariant(
                errors,
                "/briefingCards",
                "Cold Start requires exactly ten cards and four slots per agent.");
        }

        var viewBox = mission.Authoring.Presentation.ViewBox;
        foreach (var room in mission.Authoring.Rooms)
        {
            var halfWidth = room.Visual.Size.W / 2;
            var halfHeight = room.Visual.Size.H / 2;
            if (room.Visual.Anchor.X - halfWidth < 0
                || room.Visual.Anchor.Y - halfHeight < 0
                || room.Visual.Anchor.X + halfWidth > viewBox.Width
                || room.Visual.Anchor.Y + halfHeight > viewBox.Height)
            {
                AddInvariant(
                    errors,
                    $"/rooms/{room.RoomId}/visual",
                    $"Room '{room.RoomId}' extends beyond the declared viewBox.");
            }
        }
    }

    private static void ValidateTopology(
        RunDefinition definition,
        VariantDocument variant,
        int roleAssignments,
        int safeWindows,
        List<ValidationError> errors)
    {
        var recon = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Recon);
        var engineer = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Engineer);
        var requiredReconRooms = new[]
        {
            definition.ConsoleAlpha.RoomId,
            definition.ConsoleBeta.RoomId,
            definition.Recorder.ArchiveRoomId,
            definition.ExtractionRoomId,
        };
        var requiredEngineerRooms = new[]
        {
            definition.Generator.RoomId,
            definition.ConsoleAlpha.RoomId,
            definition.ConsoleBeta.RoomId,
            definition.Recorder.ArchiveRoomId,
            definition.ExtractionRoomId,
        };

        if (!HasPath(definition, engineer, engineer.StartRoomId, definition.Generator.RoomId, false, false))
        {
            AddVariantInvariant(variant, errors, "engineer cannot reach the generator before power");
        }

        if (requiredReconRooms.Any(roomId => !HasPath(
                definition,
                recon,
                recon.StartRoomId,
                roomId,
                true,
                true)))
        {
            AddVariantInvariant(variant, errors, "recon cannot reach every required room");
        }

        if (requiredEngineerRooms.Any(roomId => !HasPath(
                definition,
                engineer,
                engineer.StartRoomId,
                roomId,
                true,
                true)))
        {
            AddVariantInvariant(variant, errors, "engineer cannot reach every required room");
        }

        if (!HasPath(
                definition,
                recon,
                definition.Recorder.ArchiveRoomId,
                definition.ExtractionRoomId,
                true,
                true)
            && !HasPath(
                definition,
                engineer,
                definition.Recorder.ArchiveRoomId,
                definition.ExtractionRoomId,
                true,
                true))
        {
            AddVariantInvariant(variant, errors, "recorder has no extraction route");
        }

        if (roleAssignments < 2)
        {
            AddVariantInvariant(variant, errors, "fewer than two console role assignments are reachable");
        }

        if (safeWindows < 2)
        {
            AddVariantInvariant(variant, errors, "fewer than two patrol-safe sync windows exist");
        }
    }

    private static int CountConsoleRoleAssignments(RunDefinition definition)
    {
        var alpha = definition.ConsoleAlpha.RoomId;
        var beta = definition.ConsoleBeta.RoomId;
        var first = definition.Agents[0];
        var second = definition.Agents[1];
        var firstAssignment = HasPath(definition, first, first.StartRoomId, alpha, true, false)
            && HasPath(definition, second, second.StartRoomId, beta, true, false);
        var secondAssignment = HasPath(definition, first, first.StartRoomId, beta, true, false)
            && HasPath(definition, second, second.StartRoomId, alpha, true, false);

        return (firstAssignment ? 1 : 0) + (secondAssignment ? 1 : 0);
    }

    private static int CountSafeSyncWindows(RunDefinition definition)
    {
        var unsafeRooms = new HashSet<RoomId>
        {
            definition.ConsoleAlpha.RoomId,
            definition.ConsoleBeta.RoomId,
        };

        return Enumerable.Range(1, definition.Rules.TurnLimit)
            .Count(turn => !unsafeRooms.Contains(
                definition.Drone.PatrolRoute[
                    (definition.Drone.InitialRouteIndex + turn) % definition.Drone.PatrolRoute.Length]));
    }

    private static bool HasPath(
        RunDefinition definition,
        AgentDefinition agent,
        RoomId from,
        RoomId to,
        bool powerOnline,
        bool archiveOpen)
    {
        if (from == to)
        {
            return true;
        }

        var pending = new Queue<RoomId>();
        var visited = new HashSet<RoomId> { from };
        pending.Enqueue(from);

        while (pending.TryDequeue(out var roomId))
        {
            foreach (var connection in definition.Connections.Where(connection =>
                         connection.RoomA == roomId || connection.RoomB == roomId))
            {
                if (connection.Access == ConnectionAccess.ReconCrawlspace
                    && !agent.Capabilities.HasFlag(AgentCapabilities.UseCrawlspace)
                    || connection.Access == ConnectionAccess.PowerServiceLock && !powerOnline
                    || connection.Access == ConnectionAccess.ArchiveGate && !archiveOpen)
                {
                    continue;
                }

                var next = connection.RoomA == roomId ? connection.RoomB : connection.RoomA;
                if (next == to)
                {
                    return true;
                }

                if (visited.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }

        return false;
    }

    private static void AddSolverError(
        VariantDocument variant,
        string proofName,
        ReferenceSolution solution,
        List<ValidationError> errors)
    {
        if (!solution.Solved
            || solution.CompletionTurn is null
            || solution.CompletionTurn > 17
            || proofName == "no-damage" && solution.DamageTaken != 0)
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentUnsolvedVariant,
                    $"/variants/{variant.VariantId}",
                    $"Variant '{variant.VariantId}' failed its {proofName} proof: "
                    + (solution.Failure ?? $"completed on turn {solution.CompletionTurn}.")));
        }
    }

    private static void AddVariantInvariant(
        VariantDocument variant,
        List<ValidationError> errors,
        string message) => AddInvariant(
            errors,
            $"/variants/{variant.VariantId}",
            $"Variant '{variant.VariantId}' is invalid: {message}.");

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
}
