using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using DirectiveDrift.Application;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Serialization;
using DirectiveDrift.Core.Simulation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DirectiveDrift.Persistence;

public sealed class EfGameRepository(
    IDbContextFactory<GameDbContext> contextFactory,
    TimeProvider timeProvider) : IGameRepository, IMasteryRepository, IDecisionCheckpointStore
{
    private static readonly JsonSerializerOptions StorageJson = CreateStorageJson();

    public async Task<bool> GuestExistsAsync(
        string ownerId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.GuestProfiles.AsNoTracking().AnyAsync(
            entity => entity.Id == ownerId,
            cancellationToken);
    }

    public async Task EnsureGuestAsync(string ownerId, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await database.GuestProfiles.AnyAsync(entity => entity.Id == ownerId, cancellationToken))
        {
            database.GuestProfiles.Add(
                new GuestProfileEntity { Id = ownerId, CreatedAt = timeProvider.GetUtcNow() });
            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                database.ChangeTracker.Clear();
                if (!await database.GuestProfiles.AnyAsync(
                        entity => entity.Id == ownerId,
                        cancellationToken))
                {
                    throw;
                }
            }
        }
    }

    public async Task<BuildVersionSnapshot> CreateBuildAsync(
        string ownerId,
        string buildId,
        string missionId,
        string name,
        string canonicalJson,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.Builds.Add(
            new BuildEntity
            {
                Id = buildId,
                OwnerId = ownerId,
                MissionId = missionId,
                Name = name,
                LatestVersion = 1,
                CreatedAt = now,
            });
        var version = new BuildVersionEntity
        {
            BuildId = buildId,
            Version = 1,
            CanonicalJson = canonicalJson,
            HasBeenUsed = false,
            CreatedAt = now,
        };
        database.BuildVersions.Add(version);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqliteException
            {
                SqliteExtendedErrorCode: 1555 or 2067,
            })
        {
            throw new ResourceConflictException("The build ID is already in use.", exception);
        }
        return ToSnapshot(version);
    }

    public async Task<ImmutableArray<BuildSummary>> ListBuildsAsync(
        string ownerId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var values = await database.Builds
            .AsNoTracking()
            .Where(entity => entity.OwnerId == ownerId)
            .OrderBy(entity => entity.Id)
            .Select(entity => new BuildSummary(
                entity.Id,
                entity.MissionId,
                entity.Name,
                entity.LatestVersion,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return values.ToImmutableArray();
    }

    public async Task<BuildVersionSnapshot?> GetBuildVersionAsync(
        string ownerId,
        string buildId,
        int version,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await database.BuildVersions
            .AsNoTracking()
            .Where(candidate => candidate.BuildId == buildId && candidate.Version == version)
            .Join(
                database.Builds.Where(build => build.OwnerId == ownerId),
                candidate => candidate.BuildId,
                build => build.Id,
                (candidate, _) => candidate)
            .SingleOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToSnapshot(entity);
    }

    public async Task<ImmutableArray<BuildVersionSnapshot>> ListBuildVersionsAsync(
        string ownerId,
        string buildId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var values = await database.BuildVersions
            .AsNoTracking()
            .Where(entity => entity.BuildId == buildId)
            .Join(
                database.Builds.Where(build => build.OwnerId == ownerId),
                entity => entity.BuildId,
                build => build.Id,
                (entity, _) => entity)
            .OrderBy(entity => entity.Version)
            .ToArrayAsync(cancellationToken);
        return values.Select(ToSnapshot).ToImmutableArray();
    }

    public async Task<BuildVersionSnapshot?> AddBuildVersionAsync(
        string ownerId,
        string buildId,
        string canonicalJson,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var build = await database.Builds.SingleOrDefaultAsync(
            entity => entity.Id == buildId && entity.OwnerId == ownerId,
            cancellationToken);
        if (build is null)
        {
            return null;
        }

        build.LatestVersion++;
        var version = new BuildVersionEntity
        {
            BuildId = buildId,
            Version = build.LatestVersion,
            CanonicalJson = canonicalJson,
            HasBeenUsed = false,
            CreatedAt = now,
        };
        database.BuildVersions.Add(version);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToSnapshot(version);
    }

    public async Task<RunSummary> CreateRunAsync(
        string ownerId,
        PreparedRun preparedRun,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var version = await database.BuildVersions
            .Join(
                database.Builds.Where(build => build.OwnerId == ownerId),
                candidate => candidate.BuildId,
                build => build.Id,
                (candidate, _) => candidate)
            .SingleAsync(
                candidate => candidate.BuildId == preparedRun.BuildId
                    && candidate.Version == preparedRun.BuildVersion,
                cancellationToken);
        version.HasBeenUsed = true;

        var stateHash = CanonicalStateSerializer.Hash(preparedRun.InitialState);
        var run = new RunEntity
        {
            Id = preparedRun.RunId.Value,
            OwnerId = ownerId,
            BuildId = preparedRun.BuildId,
            BuildVersion = preparedRun.BuildVersion,
            MissionId = preparedRun.InitialState.Mission.MissionId.Value,
            VariantId = preparedRun.InitialState.Mission.VariantId.Value,
            Turn = preparedRun.InitialState.Turn,
            Status = (int)preparedRun.InitialState.Status,
            StateHash = stateHash,
            ProviderProfileId = preparedRun.ProviderProfileId,
            ScriptedPlanJson = SerializePlan(preparedRun.ScriptedPlan),
            Kind = (int)preparedRun.Kind,
            Assisted = preparedRun.InitialState.Score.Assisted,
            CertificationId = preparedRun.CertificationId,
            VariantDisclosureJson = preparedRun.VariantDisclosureJson,
            CreatedAt = now,
            UpdatedAt = now,
        };
        database.Runs.Add(run);
        database.RunSnapshots.Add(
            new RunSnapshotEntity
            {
                RunId = run.Id,
                Turn = 0,
                StateJson = CanonicalStateSerializer.Serialize(preparedRun.InitialState),
                StateHash = stateHash,
                CreatedAt = now,
            });
        database.DomainEvents.Add(ToEventEntity(run.Id, preparedRun.InitialEvent));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToSummary(run);
    }

    public async Task<RunSummary?> GetRunAsync(
        string ownerId,
        RunId runId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await database.Runs.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.Id == runId.Value && entity.OwnerId == ownerId,
            cancellationToken);
        return run is null ? null : ToSummary(run);
    }

    public async Task<CertificationSummary> CreateCertificationAsync(
        string ownerId,
        string certificationId,
        string buildId,
        int buildVersion,
        string providerProfileId,
        string missionContentVersion,
        string rulesVersion,
        string scoreVersion,
        string certificationVersion,
        IReadOnlyList<PreparedRun> runs,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (runs.Count != 3
            || runs.Select(run => run.InitialState.Mission.VariantId).Distinct().Count() != 3
            || runs.Any(run => run.Kind != RunKind.Certification
                || !string.Equals(run.CertificationId, certificationId, StringComparison.Ordinal)
                || run.BuildId != buildId
                || run.BuildVersion != buildVersion
                || run.ProviderProfileId != providerProfileId))
        {
            throw new ArgumentException("A certification requires three distinct locked runs.", nameof(runs));
        }

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var build = await database.BuildVersions
            .Join(
                database.Builds.Where(value => value.OwnerId == ownerId),
                value => value.BuildId,
                value => value.Id,
                (value, _) => value)
            .SingleAsync(
                value => value.BuildId == buildId && value.Version == buildVersion,
                cancellationToken);
        build.HasBeenUsed = true;
        database.Certifications.Add(new CertificationEntity
        {
            Id = certificationId,
            OwnerId = ownerId,
            Status = "active",
            BuildId = buildId,
            BuildVersion = buildVersion,
            ProviderProfileId = providerProfileId,
            MissionContentVersion = missionContentVersion,
            RulesVersion = rulesVersion,
            ScoreVersion = scoreVersion,
            CertificationVersion = certificationVersion,
            CreatedAt = now,
        });

        for (var slot = 0; slot < runs.Count; slot++)
        {
            var prepared = runs[slot];
            var stateHash = CanonicalStateSerializer.Hash(prepared.InitialState);
            var entity = new RunEntity
            {
                Id = prepared.RunId.Value,
                OwnerId = ownerId,
                BuildId = buildId,
                BuildVersion = buildVersion,
                MissionId = prepared.InitialState.Mission.MissionId.Value,
                VariantId = prepared.InitialState.Mission.VariantId.Value,
                Turn = prepared.InitialState.Turn,
                Status = (int)prepared.InitialState.Status,
                StateHash = stateHash,
                ProviderProfileId = providerProfileId,
                ScriptedPlanJson = SerializePlan(prepared.ScriptedPlan),
                Kind = (int)RunKind.Certification,
                Assisted = false,
                CertificationId = certificationId,
                VariantDisclosureJson = prepared.VariantDisclosureJson,
                CreatedAt = now,
                UpdatedAt = now,
            };
            database.Runs.Add(entity);
            database.RunSnapshots.Add(new RunSnapshotEntity
            {
                RunId = entity.Id,
                Turn = 0,
                StateJson = CanonicalStateSerializer.Serialize(prepared.InitialState),
                StateHash = stateHash,
                CreatedAt = now,
            });
            database.DomainEvents.Add(ToEventEntity(entity.Id, prepared.InitialEvent));
            database.CertificationRuns.Add(new CertificationRunEntity
            {
                CertificationId = certificationId,
                RunId = entity.Id,
                Slot = slot + 1,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetCertificationAsync(ownerId, certificationId, cancellationToken))!;
    }

    public async Task<CertificationSummary?> GetCertificationAsync(
        string ownerId,
        string certificationId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var certificate = await database.Certifications.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == certificationId && value.OwnerId == ownerId,
            cancellationToken);
        if (certificate is null)
        {
            return null;
        }

        var rows = await database.CertificationRuns.AsNoTracking()
            .Where(value => value.CertificationId == certificationId)
            .Join(database.Runs.AsNoTracking(), value => value.RunId, value => value.Id, (link, run) => new { link.Slot, Run = run })
            .OrderBy(value => value.Slot)
            .ToArrayAsync(cancellationToken);
        var revealed = !string.Equals(certificate.Status, "active", StringComparison.Ordinal);
        var successes = rows.Count(value => (RunStatus)value.Run.Status == RunStatus.Succeeded);
        return new CertificationSummary(
            certificate.Id,
            certificate.BuildId,
            certificate.BuildVersion,
            certificate.ProviderProfileId,
            certificate.MissionContentVersion,
            certificate.RulesVersion,
            certificate.ScoreVersion,
            certificate.CertificationVersion,
            certificate.Status,
            successes,
            revealed,
            string.Equals(certificate.Status, "passed", StringComparison.Ordinal)
                ? ["robust-build"]
                : [],
            certificate.CreatedAt,
            certificate.CompletedAt,
            rows.Select(value => new CertificationRunSummary(
                value.Slot,
                new RunId(value.Run.Id),
                (RunStatus)value.Run.Status,
                (RunStatus)value.Run.Status == RunStatus.Active
                    ? null
                    : (RunStatus)value.Run.Status == RunStatus.Succeeded,
                revealed ? value.Run.VariantDisclosureJson : null)).ToImmutableArray());
    }

    public async Task<bool> HasCertificationEligibilityAsync(
        string ownerId,
        string buildId,
        int buildVersion,
        string providerProfileId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var distinctVariants = await database.Runs.AsNoTracking()
            .Where(value => value.OwnerId == ownerId
                && value.BuildId == buildId
                && value.BuildVersion == buildVersion
                && value.ProviderProfileId == providerProfileId
                && value.Kind == (int)RunKind.Practice
                && !value.Assisted
                && value.Status == (int)RunStatus.Succeeded)
            .Select(value => value.VariantId)
            .Distinct()
            .CountAsync(cancellationToken);
        return distinctVariants >= 3;
    }

    public async Task<RunComparison?> GetComparisonAsync(
        string ownerId,
        RunId leftRunId,
        RunId rightRunId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var runs = await database.Runs.AsNoTracking()
            .Where(value => value.OwnerId == ownerId
                && (value.Id == leftRunId.Value || value.Id == rightRunId.Value))
            .ToArrayAsync(cancellationToken);
        if (runs.Length != 2 || runs.Any(value => value.Kind == (int)RunKind.Certification && value.CertificationId != null))
        {
            return null;
        }

        var left = runs.Single(value => value.Id == leftRunId.Value);
        var right = runs.Single(value => value.Id == rightRunId.Value);
        if (left.Assisted || right.Assisted)
        {
            return null;
        }

        var builds = await database.BuildVersions.AsNoTracking()
            .Where(value => value.BuildId == left.BuildId && value.Version == left.BuildVersion
                || value.BuildId == right.BuildId && value.Version == right.BuildVersion)
            .ToArrayAsync(cancellationToken);
        var leftBuild = builds.Single(value => value.BuildId == left.BuildId && value.Version == left.BuildVersion);
        var rightBuild = builds.Single(value => value.BuildId == right.BuildId && value.Version == right.BuildVersion);
        var decisions = await database.DecisionRecords.AsNoTracking()
            .Where(value => value.RunId == left.Id || value.RunId == right.Id)
            .OrderBy(value => value.Turn).ThenBy(value => value.AgentId)
            .ToArrayAsync(cancellationToken);
        var leftDecisions = decisions.Where(value => value.RunId == left.Id)
            .ToDictionary(value => (value.Turn, value.AgentId), value => value.ActionId);
        var rightDecisions = decisions.Where(value => value.RunId == right.Id)
            .ToDictionary(value => (value.Turn, value.AgentId), value => value.ActionId);
        var first = leftDecisions.Keys.Union(rightDecisions.Keys)
            .OrderBy(value => value.Turn).ThenBy(value => value.AgentId, StringComparer.Ordinal)
            .Select(key => new { key, Left = leftDecisions.GetValueOrDefault(key), Right = rightDecisions.GetValueOrDefault(key) })
            .FirstOrDefault(value => !string.Equals(value.Left, value.Right, StringComparison.Ordinal));
        var buildDifference = CompareBuildJson(leftBuild.CanonicalJson, rightBuild.CanonicalJson);
        var costs = await database.UsageLedger.AsNoTracking()
            .Where(value => value.RunId == left.Id || value.RunId == right.Id)
            .GroupBy(value => value.RunId)
            .Select(group => new { RunId = group.Key, Cost = group.Sum(value => value.ActualCostMicros) })
            .ToDictionaryAsync(value => value.RunId, value => value.Cost, cancellationToken);
        return new RunComparison(
            ToSummary(left),
            ToSummary(right),
            buildDifference,
            first is null ? null : new DecisionDifference(first.key.Turn, new AgentId(first.key.AgentId), first.Left, first.Right),
            await ScoreAsync(database, left.Id, cancellationToken),
            await ScoreAsync(database, right.Id, cancellationToken),
            costs.GetValueOrDefault(left.Id),
            costs.GetValueOrDefault(right.Id));
    }

    public async Task<PlayerUsageAllowance> GetUsageAllowanceAsync(
        string ownerId,
        int dailyLimitMicros,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var entries = await database.UsageLedger.AsNoTracking()
            .Where(value => value.OwnerId == ownerId)
            .ToArrayAsync(cancellationToken);
        var used = entries.Where(value => value.CreatedAt >= day)
            .Sum(value => value.Status == "settled" ? value.ActualCostMicros : value.ReservedCostMicros);
        return new PlayerUsageAllowance(dailyLimitMicros, used, Math.Max(0, dailyLimitMicros - used), int.MaxValue);
    }

    public async Task<InternalRunDiagnostics?> GetRunDiagnosticsAsync(
        string ownerId,
        RunId runId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await database.Runs.AnyAsync(value => value.Id == runId.Value && value.OwnerId == ownerId, cancellationToken))
        {
            return null;
        }
        var rows = await database.UsageLedger.AsNoTracking().Where(value => value.RunId == runId.Value).ToArrayAsync(cancellationToken);
        var attempts = await database.ProviderDecisionCheckpoints.AsNoTracking()
            .Where(value => database.TurnOperations.Where(operation => operation.RunId == runId.Value).Select(operation => operation.Id).Contains(value.OperationId))
            .SumAsync(value => value.AttemptCount, cancellationToken);
        return new InternalRunDiagnostics(
            runId,
            rows.Sum(value => value.ActualInputTokens),
            rows.Sum(value => value.ActualOutputTokens),
            rows.Sum(value => value.ActualCostMicros),
            attempts);
    }

    public async Task<RunSummary?> ApplyEmergencyBurstAsync(
        string ownerId,
        RunId runId,
        string text,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var run = await database.Runs.SingleOrDefaultAsync(value => value.Id == runId.Value && value.OwnerId == ownerId, cancellationToken);
        if (run is null)
        {
            return null;
        }
        if (run.Kind != (int)RunKind.Practice || run.Assisted
            || await database.TurnOperations.AnyAsync(value => value.RunId == run.Id && (value.Status == 0 || value.Status == 1), cancellationToken))
        {
            throw new ResourceConflictException(
                "Emergency Burst is unavailable for this run.",
                new InvalidOperationException("Run is not eligible for intervention."));
        }
        var snapshot = await database.RunSnapshots.SingleAsync(value => value.RunId == run.Id && value.Turn == run.Turn, cancellationToken);
        var intervention = RunIntervention.ApplyEmergencyBurst(CanonicalStateSerializer.Deserialize(snapshot.StateJson), text);
        snapshot.StateJson = CanonicalStateSerializer.Serialize(intervention.State);
        snapshot.StateHash = intervention.StateHash;
        run.StateHash = intervention.StateHash;
        run.Assisted = true;
        run.UpdatedAt = now;
        database.DomainEvents.AddRange(intervention.Events.Select(value => ToEventEntity(run.Id, value)));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToSummary(run);
    }

    public async Task<EnqueueTurnResult?> EnqueueTurnAsync(
        string ownerId,
        RunId runId,
        string idempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await database.Runs.SingleOrDefaultAsync(
            entity => entity.Id == runId.Value && entity.OwnerId == ownerId,
            cancellationToken);
        if (run is null)
        {
            return null;
        }

        var existing = await database.TurnOperations.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.RunId == run.Id
                && entity.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return new EnqueueTurnResult(ToSummary(existing), true, false);
        }

        var turn = run.Turn + 1;
        var active = await database.TurnOperations.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.RunId == run.Id
                && (entity.Status == (int)TurnOperationStatus.Queued
                    || entity.Status == (int)TurnOperationStatus.Processing),
            cancellationToken);
        if (active is not null || (RunStatus)run.Status != RunStatus.Active)
        {
            var conflict = active ?? new TurnOperationEntity
            {
                Id = string.Empty,
                RunId = run.Id,
                Turn = turn,
                IdempotencyKey = idempotencyKey,
                Status = (int)TurnOperationStatus.Failed,
                ErrorCode = "run-not-advanceable",
                CreatedAt = now,
                UpdatedAt = now,
            };
            return new EnqueueTurnResult(ToSummary(conflict), false, true);
        }

        var operation = new TurnOperationEntity
        {
            Id = CreateId("op"),
            RunId = run.Id,
            Turn = turn,
            IdempotencyKey = idempotencyKey,
            Status = (int)TurnOperationStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now,
        };
        database.TurnOperations.Add(operation);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return new EnqueueTurnResult(ToSummary(operation), false, false);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            var raced = await database.TurnOperations.AsNoTracking()
                .Where(entity => entity.RunId == run.Id)
                .OrderByDescending(entity => entity.Id)
                .FirstAsync(cancellationToken);
            var replay = string.Equals(
                raced.IdempotencyKey,
                idempotencyKey,
                StringComparison.Ordinal);
            return new EnqueueTurnResult(ToSummary(raced), replay, !replay);
        }
    }

    public async Task<TurnOperationSummary?> GetOperationAsync(
        string ownerId,
        string operationId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var operation = await database.TurnOperations
            .AsNoTracking()
            .Where(entity => entity.Id == operationId)
            .Join(
                database.Runs.Where(run => run.OwnerId == ownerId),
                entity => entity.RunId,
                run => run.Id,
                (entity, _) => entity)
            .SingleOrDefaultAsync(cancellationToken);
        return operation is null ? null : ToSummary(operation);
    }

    public async Task<ClaimedTurnOperation?> ClaimNextTurnAsync(
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var nowUnixMilliseconds = now.ToUnixTimeMilliseconds();
        var leaseExpiresAtUnixMilliseconds = (now + leaseDuration).ToUnixTimeMilliseconds();
        var candidate = await database.TurnOperations
            .AsNoTracking()
            .Where(entity => entity.Status == (int)TurnOperationStatus.Queued
                || entity.Status == (int)TurnOperationStatus.Processing
                    && entity.LeaseExpiresAtUnixMilliseconds < nowUnixMilliseconds)
            .OrderBy(entity => entity.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var leaseToken = CreateId("lease");
        var claimed = await database.TurnOperations
            .Where(entity => entity.Id == candidate.Id
                && (entity.Status == (int)TurnOperationStatus.Queued
                    || entity.Status == (int)TurnOperationStatus.Processing
                        && entity.LeaseExpiresAtUnixMilliseconds < nowUnixMilliseconds))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entity => entity.Status, (int)TurnOperationStatus.Processing)
                    .SetProperty(entity => entity.LeaseToken, leaseToken)
                    .SetProperty(
                        entity => entity.LeaseExpiresAtUnixMilliseconds,
                        leaseExpiresAtUnixMilliseconds)
                    .SetProperty(entity => entity.HeartbeatUnixMilliseconds, nowUnixMilliseconds)
                    .SetProperty(entity => entity.AttemptCount, entity => entity.AttemptCount + 1)
                    .SetProperty(entity => entity.UpdatedAt, now),
                cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        var operation = await database.TurnOperations.AsNoTracking().SingleAsync(
            entity => entity.Id == candidate.Id,
            cancellationToken);
        var run = await database.Runs.AsNoTracking().SingleAsync(
            entity => entity.Id == operation.RunId,
            cancellationToken);
        var snapshot = await database.RunSnapshots.AsNoTracking().SingleAsync(
            entity => entity.RunId == run.Id && entity.Turn == operation.Turn - 1,
            cancellationToken);
        var build = await database.BuildVersions.AsNoTracking().SingleAsync(
            entity => entity.BuildId == run.BuildId && entity.Version == run.BuildVersion,
            cancellationToken);
        var plan = DeserializePlan(run.ScriptedPlanJson);
        if (!plan.TryGetValue(operation.Turn, out var actions))
        {
            throw new InvalidOperationException($"Scripted plan has no turn {operation.Turn}.");
        }

        return new ClaimedTurnOperation(
            operation.Id,
            leaseToken,
            run.OwnerId,
            new RunId(run.Id),
            operation.Turn,
            CanonicalStateSerializer.Deserialize(snapshot.StateJson),
            actions,
            build.CanonicalJson,
            run.ProviderProfileId);
    }

    public async Task CompleteTurnAsync(
        ClaimedTurnOperation operation,
        TurnResult result,
        UsageSettlement settlement,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var operationEntity = await database.TurnOperations.SingleAsync(
            entity => entity.Id == operation.OperationId
                && entity.Status == (int)TurnOperationStatus.Processing
                && entity.LeaseToken == operation.LeaseToken,
            cancellationToken);
        var run = await database.Runs.SingleAsync(
            entity => entity.Id == operation.RunId.Value && entity.Turn == operation.Turn - 1,
            cancellationToken);

        run.Turn = result.State.Turn;
        run.Status = (int)result.State.Status;
        run.StateHash = result.StateHash;
        run.Assisted = result.State.Score.Assisted;
        run.UpdatedAt = now;
        operationEntity.Status = (int)TurnOperationStatus.Succeeded;
        operationEntity.LeaseExpiresAtUnixMilliseconds = null;
        operationEntity.HeartbeatUnixMilliseconds = now.ToUnixTimeMilliseconds();
        operationEntity.UpdatedAt = now;
        database.RunSnapshots.Add(
            new RunSnapshotEntity
            {
                RunId = run.Id,
                Turn = result.State.Turn,
                StateJson = CanonicalStateSerializer.Serialize(result.State),
                StateHash = result.StateHash,
                CreatedAt = now,
            });
        database.DecisionRecords.AddRange(
            result.Decisions.Select(decision => new DecisionRecordEntity
            {
                RunId = run.Id,
                Turn = result.State.Turn,
                AgentId = decision.AgentId.Value,
                OperationId = operation.OperationId,
                ActionId = decision.Action.ActionId.Value,
                DecisionJson = JsonSerializer.Serialize(decision, StorageJson),
            }));
        database.DomainEvents.AddRange(result.Events.Select(value => ToEventEntity(run.Id, value)));
        var usage = await database.UsageLedger.SingleAsync(
            entity => entity.Id == settlement.ReservationId
                && entity.OperationId == operation.OperationId
                && entity.Status == "reserved",
            cancellationToken);
        usage.ActualInputTokens = settlement.InputTokens;
        usage.ActualOutputTokens = settlement.OutputTokens;
        usage.ActualCostMicros = settlement.CostMicros;
        usage.Status = "settled";
        usage.UpdatedAt = now;
        var certification = await database.Certifications.SingleOrDefaultAsync(
            value => value.Id == run.CertificationId,
            cancellationToken);
        if (certification is not null && result.State.Status != RunStatus.Active)
        {
            var linkedStatuses = await database.CertificationRuns
                .Where(value => value.CertificationId == certification.Id && value.RunId != run.Id)
                .Join(database.Runs, value => value.RunId, value => value.Id, (_, value) => value.Status)
                .ToArrayAsync(cancellationToken);
            var statuses = linkedStatuses.Append((int)result.State.Status).ToArray();
            if (statuses.All(value => value != (int)RunStatus.Active))
            {
                certification.Status = statuses.Count(value => value == (int)RunStatus.Succeeded) >= 2 ? "passed" : "failed";
                certification.CompletedAt = now;
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task FailTurnAsync(
        ClaimedTurnOperation operation,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await database.TurnOperations
            .Where(entity => entity.Id == operation.OperationId
                && entity.Status == (int)TurnOperationStatus.Processing
                && entity.LeaseToken == operation.LeaseToken)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entity => entity.Status, (int)TurnOperationStatus.Failed)
                    .SetProperty(entity => entity.ErrorCode, errorCode)
                    .SetProperty(entity => entity.LeaseExpiresAtUnixMilliseconds, (long?)null)
                    .SetProperty(entity => entity.UpdatedAt, now),
                cancellationToken);
    }

    public async Task<ImmutableArray<CanonicalEvent>?> GetEventsAsync(
        string ownerId,
        RunId runId,
        long afterSequence,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await database.Runs.AnyAsync(
                entity => entity.Id == runId.Value && entity.OwnerId == ownerId,
                cancellationToken))
        {
            return null;
        }

        var json = await database.DomainEvents
            .AsNoTracking()
            .Where(entity => entity.RunId == runId.Value && entity.Sequence > afterSequence)
            .OrderBy(entity => entity.Sequence)
            .Take(limit)
            .Select(entity => entity.EventJson)
            .ToArrayAsync(cancellationToken);
        return json.Select(DeserializeEvent).ToImmutableArray();
    }

    public async Task<ReplayData?> GetReplayAsync(
        string ownerId,
        RunId runId,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await database.Runs.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.Id == runId.Value && entity.OwnerId == ownerId,
            cancellationToken);
        if (run is null)
        {
            return null;
        }

        var build = await database.BuildVersions.AsNoTracking().SingleAsync(
            entity => entity.BuildId == run.BuildId && entity.Version == run.BuildVersion,
            cancellationToken);
        var initial = await database.RunSnapshots.AsNoTracking().SingleAsync(
            entity => entity.RunId == run.Id && entity.Turn == 0,
            cancellationToken);
        var eventsJson = await database.DomainEvents.AsNoTracking()
            .Where(entity => entity.RunId == run.Id)
            .OrderBy(entity => entity.Sequence)
            .Select(entity => entity.EventJson)
            .ToArrayAsync(cancellationToken);
        var decisionsJson = await database.DecisionRecords.AsNoTracking()
            .Where(entity => entity.RunId == run.Id)
            .OrderBy(entity => entity.Turn)
            .ThenBy(entity => entity.AgentId)
            .Select(entity => entity.DecisionJson)
            .ToArrayAsync(cancellationToken);

        return new ReplayData(
            ToSummary(run),
            ToSnapshot(build),
            CanonicalStateSerializer.Deserialize(initial.StateJson),
            eventsJson.Select(DeserializeEvent).ToImmutableArray(),
            decisionsJson.Select(DeserializeDecision).ToImmutableArray());
    }

    public async Task<ProviderDecisionResult?> GetAcceptedAsync(
        string operationId,
        AgentId agentId,
        string providerProfileId,
        string preDecisionStateHash,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var json = await database.ProviderDecisionCheckpoints
            .AsNoTracking()
            .Where(value => value.OperationId == operationId
                && value.AgentId == agentId.Value
                && value.ProviderProfileId == providerProfileId
                && value.PreDecisionStateHash == preDecisionStateHash)
            .Select(value => value.ResultJson)
            .SingleOrDefaultAsync(cancellationToken);
        return json is null
            ? null
            : JsonSerializer.Deserialize<ProviderDecisionResult>(json, StorageJson)
                ?? throw new JsonException("Stored provider result was null.");
    }

    public async Task SaveAsync(
        string operationId,
        AgentId agentId,
        string providerProfileId,
        string preDecisionStateHash,
        ProviderDecisionResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        database.ProviderDecisionCheckpoints.Add(
            new ProviderDecisionCheckpointEntity
            {
                OperationId = operationId,
                AgentId = agentId.Value,
                ProviderProfileId = providerProfileId,
                PreDecisionStateHash = preDecisionStateHash,
                ResultJson = JsonSerializer.Serialize(result, StorageJson),
                ContextJson = result.ContextJson,
                PromptTemplateHash = result.PromptTemplateHash,
                DiagnosticCode = result.DiagnosticCode,
                Status = (int)result.Status,
                InputTokens = result.Usage.InputTokens,
                OutputTokens = result.Usage.OutputTokens,
                CostMicros = result.Usage.CostMicros,
                LatencyMilliseconds = result.LatencyMilliseconds,
                AttemptCount = result.AttemptCount,
                CreatedAt = now,
            });
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            if (!await database.ProviderDecisionCheckpoints.AnyAsync(
                    value => value.OperationId == operationId && value.AgentId == agentId.Value,
                    cancellationToken))
            {
                throw;
            }
        }
    }

    private static BuildVersionSnapshot ToSnapshot(BuildVersionEntity entity) =>
        new(entity.BuildId, entity.Version, entity.CanonicalJson, entity.HasBeenUsed, entity.CreatedAt);

    private static RunSummary ToSummary(RunEntity entity) =>
        new(
            new RunId(entity.Id),
            entity.BuildId,
            entity.BuildVersion,
            new MissionId(entity.MissionId),
            new VariantId(entity.VariantId),
            entity.Turn,
            (RunStatus)entity.Status,
            entity.StateHash,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ProviderProfileId,
            (RunKind)entity.Kind,
            entity.Assisted,
            entity.CertificationId,
            entity.VariantDisclosureJson);

    private static async Task<int> ScoreAsync(GameDbContext database, string runId, CancellationToken cancellationToken)
    {
        var terminal = await database.DomainEvents.AsNoTracking()
            .Where(value => value.RunId == runId && value.EventType == nameof(CanonicalEventType.MissionSucceeded))
            .Select(value => value.EventJson)
            .SingleOrDefaultAsync(cancellationToken);
        return terminal is null ? 0 : ((MissionTerminalPayload)DeserializeEvent(terminal).Payload).Score ?? 0;
    }

    private static BuildDifference CompareBuildJson(string leftJson, string rightJson)
    {
        using var left = JsonDocument.Parse(leftJson);
        using var right = JsonDocument.Parse(rightJson);
        var leftRoot = left.RootElement;
        var rightRoot = right.RootElement;
        var ids = leftRoot.GetProperty("agents").EnumerateObject().Select(value => value.Name)
            .Union(rightRoot.GetProperty("agents").EnumerateObject().Select(value => value.Name), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        var role = ImmutableArray.CreateBuilder<AgentId>();
        var cards = ImmutableArray.CreateBuilder<AgentId>();
        var modules = ImmutableArray.CreateBuilder<AgentId>();
        foreach (var id in ids)
        {
            var leftAgent = leftRoot.GetProperty("agents").GetProperty(id);
            var rightAgent = rightRoot.GetProperty("agents").GetProperty(id);
            if (leftAgent.GetProperty("roleOrder").GetString() != rightAgent.GetProperty("roleOrder").GetString()) role.Add(new AgentId(id));
            if (leftAgent.GetProperty("briefingCardIds").GetRawText() != rightAgent.GetProperty("briefingCardIds").GetRawText()) cards.Add(new AgentId(id));
            if (leftAgent.GetProperty("moduleId").GetString() != rightAgent.GetProperty("moduleId").GetString()) modules.Add(new AgentId(id));
        }
        return new BuildDifference(
            leftRoot.GetProperty("sharedDoctrine").GetString() != rightRoot.GetProperty("sharedDoctrine").GetString(),
            role.ToImmutable(), cards.ToImmutable(), modules.ToImmutable());
    }

    private static TurnOperationSummary ToSummary(TurnOperationEntity entity) =>
        new(
            entity.Id,
            new RunId(entity.RunId),
            entity.Turn,
            entity.IdempotencyKey,
            (TurnOperationStatus)entity.Status,
            entity.ErrorCode,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static DomainEventEntity ToEventEntity(string runId, CanonicalEvent value) =>
        new()
        {
            RunId = runId,
            Sequence = value.Sequence,
            Turn = value.Turn,
            EventType = value.Type.ToString(),
            EventJson = JsonSerializer.Serialize(value, StorageJson),
        };

    private static CanonicalEvent DeserializeEvent(string json) =>
        JsonSerializer.Deserialize<CanonicalEvent>(json, StorageJson)
        ?? throw new JsonException("Stored canonical event was null.");

    private static ResolvedDecision DeserializeDecision(string json) =>
        JsonSerializer.Deserialize<ResolvedDecision>(json, StorageJson)
        ?? throw new JsonException("Stored resolved decision was null.");

    private static string SerializePlan(
        ImmutableDictionary<int, ImmutableDictionary<AgentId, ActionId>> plan) =>
        JsonSerializer.Serialize(
            plan.OrderBy(entry => entry.Key).Select(
                entry => new StoredPlanTurn(
                    entry.Key,
                    entry.Value.OrderBy(action => action.Key.Value, StringComparer.Ordinal)
                        .ToDictionary(
                            action => action.Key.Value,
                            action => action.Value.Value,
                            StringComparer.Ordinal))),
            StorageJson);

    private static ImmutableDictionary<int, ImmutableDictionary<AgentId, ActionId>> DeserializePlan(
        string json) =>
        (JsonSerializer.Deserialize<StoredPlanTurn[]>(json, StorageJson)
            ?? throw new JsonException("Stored scripted plan was null."))
        .ToImmutableDictionary(
            turn => turn.Turn,
            turn => turn.Actions.ToImmutableDictionary(
                action => new AgentId(action.Key),
                action => new ActionId(action.Value)));

    private static string CreateId(string prefix) =>
        $"{prefix}_{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";

    private static JsonSerializerOptions CreateStorageJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed record StoredPlanTurn(int Turn, IReadOnlyDictionary<string, string> Actions);
}
