using System.Collections.Immutable;
using System.Globalization;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Serialization;
using DirectiveDrift.Core.Simulation;
using DirectiveDrift.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DirectiveDrift.Persistence.Tests;

public sealed class PersistenceIntegrationTests : IAsyncLifetime
{
    private static readonly string[] ConcurrentKeys = ["concurrent-a", "concurrent-b"];

    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"directive-drift-p4-{Guid.NewGuid():N}.db");
    private IDbContextFactory<GameDbContext> contextFactory = null!;
    private EfGameRepository repository = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        contextFactory = new TestContextFactory(options);
        repository = new EfGameRepository(contextFactory, TimeProvider.System);
        await using var database = await contextFactory.CreateDbContextAsync();
        await database.Database.MigrateAsync();
        await database.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }

    public Task DisposeAsync()
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-shm", $"{databasePath}-wal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task InitialMigrationCreatesAllP4TablesAndWalDatabase()
    {
        await using var database = await contextFactory.CreateDbContextAsync();
        var tables = await database.Database.SqlQueryRaw<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type = 'table' ORDER BY name")
            .ToArrayAsync();
        var expected = new[]
        {
            "BuildVersions",
            "Builds",
            "CertificationRuns",
            "Certifications",
            "DecisionRecords",
            "DomainEvents",
            "GuestProfiles",
            "ProviderDecisionCheckpoints",
            "RunSnapshots",
            "Runs",
            "SchemaMetadata",
            "TurnOperations",
            "UsageLedger",
        };

        Assert.All(expected, table => Assert.Contains(table, tables));
        await database.Database.OpenConnectionAsync();
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var mode = (string)(await command.ExecuteScalarAsync() ?? string.Empty);
        Assert.Equal("wal", mode, ignoreCase: true);
    }

    [Fact]
    public async Task OwnershipAndImmutableUsedVersionArePersisted()
    {
        var prepared = await SeedRunAsync();

        Assert.Null(await repository.GetRunAsync("guest-other", prepared.RunId, default));
        Assert.Null(
            await repository.GetBuildVersionAsync(
                "guest-other",
                prepared.BuildId,
                prepared.BuildVersion,
                default));
        var version = await repository.GetBuildVersionAsync(
            "guest-owner",
            prepared.BuildId,
            prepared.BuildVersion,
            default);
        Assert.NotNull(version);
        Assert.True(version.HasBeenUsed);
        Assert.Equal("{\"immutable\":true}", version.CanonicalJson);
    }

    [Fact]
    public async Task DuplicateAdvanceIsIdempotentAndAnotherActiveAdvanceConflicts()
    {
        var prepared = await SeedRunAsync();
        var now = ParseTime("2026-08-09T12:00:00Z");

        var first = await repository.EnqueueTurnAsync(
            "guest-owner",
            prepared.RunId,
            "request-one",
            now,
            default);
        var duplicate = await repository.EnqueueTurnAsync(
            "guest-owner",
            prepared.RunId,
            "request-one",
            now,
            default);
        var conflict = await repository.EnqueueTurnAsync(
            "guest-owner",
            prepared.RunId,
            "request-two",
            now,
            default);

        Assert.NotNull(first);
        Assert.True(duplicate!.IsReplay);
        Assert.Equal(first.Operation.OperationId, duplicate.Operation.OperationId);
        Assert.True(conflict!.IsConflict);
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Equal(1, await database.TurnOperations.CountAsync());
    }

    [Fact]
    public async Task SimultaneousAdvanceRequestsCreateOnlyOneOperation()
    {
        var prepared = await SeedRunAsync();
        var now = ParseTime("2026-08-09T12:00:00Z");
        var requests = ConcurrentKeys
            .Select(key => repository.EnqueueTurnAsync(
                "guest-owner",
                prepared.RunId,
                key,
                now,
                default));

        var results = await Task.WhenAll(requests);

        Assert.Single(results, result => result is { IsConflict: false });
        Assert.Single(results, result => result is { IsConflict: true });
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Equal(1, await database.TurnOperations.CountAsync());
    }

    [Fact]
    public async Task ExpiredLeaseIsReclaimedAfterProcessRestart()
    {
        var prepared = await SeedRunAsync();
        var now = ParseTime("2026-08-09T12:00:00Z");
        await repository.EnqueueTurnAsync(
            "guest-owner",
            prepared.RunId,
            "restart-case",
            now,
            default);

        var firstLease = await repository.ClaimNextTurnAsync(now, TimeSpan.FromMinutes(1), default);
        var beforeExpiry = await repository.ClaimNextTurnAsync(
            now.AddSeconds(59),
            TimeSpan.FromMinutes(1),
            default);
        var afterRestart = new EfGameRepository(contextFactory, TimeProvider.System);
        var reclaimed = await afterRestart.ClaimNextTurnAsync(
            now.AddMinutes(2),
            TimeSpan.FromMinutes(1),
            default);

        Assert.NotNull(firstLease);
        Assert.Null(beforeExpiry);
        Assert.NotNull(reclaimed);
        Assert.Equal(firstLease.OperationId, reclaimed.OperationId);
        Assert.NotEqual(firstLease.LeaseToken, reclaimed.LeaseToken);
    }

    [Fact]
    public async Task StateEventsDecisionsAndUsageCommitTogether()
    {
        var prepared = await SeedRunAsync();
        var now = ParseTime("2026-08-09T12:00:00Z");
        await repository.EnqueueTurnAsync(
            "guest-owner",
            prepared.RunId,
            "atomic-case",
            now,
            default);
        var operation = await repository.ClaimNextTurnAsync(now, TimeSpan.FromMinutes(1), default);
        Assert.NotNull(operation);
        var decisions = operation.ScriptedActions.ToDictionary(
            action => action.Key,
            action => new ProposedDecision(action.Value, null, "scripted", string.Empty));
        var result = TurnResolver.ResolveTurn(operation.PreTurnState, decisions);
        var usageService = new PersistentUsageReservationService(contextFactory, TimeProvider.System);
        var reservation = await usageService.ReserveAsync(
            operation.OwnerId,
            operation.RunId,
            operation.OperationId,
            CreateProfile(),
            2,
            default);

        await repository.CompleteTurnAsync(
            operation,
            result,
            usageService.Settle(reservation, []),
            now.AddSeconds(1),
            default);

        await using var database = await contextFactory.CreateDbContextAsync();
        var persistedRun = await database.Runs.SingleAsync();
        Assert.Equal(1, persistedRun.Turn);
        Assert.Equal(2, await database.RunSnapshots.CountAsync());
        Assert.Equal(2, await database.DecisionRecords.CountAsync());
        Assert.Equal(result.Events.Length + 1, await database.DomainEvents.CountAsync());
        Assert.Equal(1, await database.UsageLedger.CountAsync());
        Assert.Equal(
            TurnOperationStatus.Succeeded,
            (TurnOperationStatus)(await database.TurnOperations.SingleAsync()).Status);

        var replay = await repository.GetReplayAsync("guest-owner", prepared.RunId, default);
        Assert.NotNull(replay);
        Assert.Equal(result.Events.Length + 1, replay.Events.Length);
        Assert.Equal(2, replay.Decisions.Length);
    }

    [Fact]
    public async Task CertificationLocksMetadataHidesVariantsAndEnforcesTwoOfThree()
    {
        await SeedRunAsync();
        var now = ParseTime("2026-08-09T14:00:00Z");
        var prepared = Enumerable.Range(1, 3)
            .Select(slot => PrepareCertificationRun(slot, "cert-test"))
            .ToArray();

        var active = await repository.CreateCertificationAsync(
            "guest-owner", "cert-test", "build-test", 1, "profile-v1",
            "content-v1", "rules-v1", "score-v1", "pool-v1", prepared, now, default);

        Assert.Equal("active", active.Status);
        Assert.False(active.Revealed);
        Assert.All(active.Runs, value => Assert.Null(value.VariantDisclosureJson));
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            var first = await database.Runs.SingleAsync(value => value.Id == "cert-run-1");
            var second = await database.Runs.SingleAsync(value => value.Id == "cert-run-2");
            first.Status = (int)RunStatus.Succeeded;
            second.Status = (int)RunStatus.Failed;
            await database.SaveChangesAsync();
        }

        await repository.EnqueueTurnAsync("guest-owner", new RunId("cert-run-3"), "cert-final", now, default);
        var claimed = await repository.ClaimNextTurnAsync(now, TimeSpan.FromMinutes(1), default);
        Assert.NotNull(claimed);
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            database.UsageLedger.Add(new UsageLedgerEntity
            {
                Id = "cert-usage",
                OwnerId = "guest-owner",
                RunId = claimed.RunId.Value,
                OperationId = claimed.OperationId,
                ReservedInputTokens = 0,
                ReservedOutputTokens = 0,
                ReservedCostMicros = 0,
                Status = "reserved",
                CreatedAt = now,
                UpdatedAt = now,
            });
            await database.SaveChangesAsync();
        }
        var succeeded = claimed.PreTurnState with { Turn = 1, Status = RunStatus.Succeeded };
        var stateHash = CanonicalStateSerializer.Hash(succeeded);
        await repository.CompleteTurnAsync(
            claimed,
            new TurnResult(succeeded, [], [], [], stateHash),
            new UsageSettlement("cert-usage", 0, 0, 0),
            now.AddSeconds(1),
            default);

        var completed = await repository.GetCertificationAsync("guest-owner", "cert-test", default);
        Assert.NotNull(completed);
        Assert.Equal("passed", completed.Status);
        Assert.Equal(2, completed.Successes);
        Assert.True(completed.Revealed);
        Assert.All(completed.Runs, value => Assert.Contains("cs-cert-", value.VariantDisclosureJson));
        Assert.Equal("profile-v1", completed.ProviderProfileId);
        Assert.Equal("pool-v1", completed.CertificationVersion);
        Assert.Equal("robust-build", Assert.Single(completed.Badges));
    }

    [Fact]
    public async Task AssistedPracticeDoesNotCountTowardCertificationEligibility()
    {
        await SeedRunAsync();
        await using var database = await contextFactory.CreateDbContextAsync();
        var first = await database.Runs.SingleAsync();
        first.Status = (int)RunStatus.Succeeded;
        first.VariantId = "practice-a";
        for (var index = 2; index <= 3; index++)
        {
            database.Runs.Add(new RunEntity
            {
                Id = $"eligibility-{index}",
                OwnerId = "guest-owner",
                BuildId = "build-test",
                BuildVersion = 1,
                MissionId = "mission-test",
                VariantId = $"practice-{index}",
                Turn = 1,
                Status = (int)RunStatus.Succeeded,
                StateHash = "hash",
                ProviderProfileId = "scripted-test",
                ScriptedPlanJson = "[]",
                Kind = (int)RunKind.Practice,
                Assisted = index == 3,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        await database.SaveChangesAsync();

        Assert.False(await repository.HasCertificationEligibilityAsync(
            "guest-owner", "build-test", 1, "scripted-test", default));
        var assisted = await database.Runs.SingleAsync(value => value.Id == "eligibility-3");
        assisted.Assisted = false;
        await database.SaveChangesAsync();
        Assert.True(await repository.HasCertificationEligibilityAsync(
            "guest-owner", "build-test", 1, "scripted-test", default));
    }

    [Fact]
    public async Task ComparisonFindsFirstDecisionAndBuildDifference()
    {
        await SeedRunAsync();
        const string firstBuild = "{\"sharedDoctrine\":\"hold\",\"agents\":{\"agent-a\":{\"roleOrder\":\"left\",\"briefingCardIds\":[\"one\"],\"moduleId\":\"none\"},\"agent-b\":{\"roleOrder\":\"anchor\",\"briefingCardIds\":[\"two\"],\"moduleId\":\"none\"}}}";
        const string secondBuild = "{\"sharedDoctrine\":\"hold\",\"agents\":{\"agent-a\":{\"roleOrder\":\"right\",\"briefingCardIds\":[\"one\"],\"moduleId\":\"none\"},\"agent-b\":{\"roleOrder\":\"anchor\",\"briefingCardIds\":[\"two\"],\"moduleId\":\"none\"}}}";
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            (await database.BuildVersions.SingleAsync()).CanonicalJson = firstBuild;
            await database.SaveChangesAsync();
        }
        await repository.AddBuildVersionAsync(
            "guest-owner", "build-test", secondBuild, ParseTime("2026-08-09T15:00:00Z"), default);
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            database.Runs.Add(new RunEntity
            {
                Id = "run-right",
                OwnerId = "guest-owner",
                BuildId = "build-test",
                BuildVersion = 2,
                MissionId = "mission-test",
                VariantId = "variant-test",
                Turn = 1,
                Status = (int)RunStatus.Succeeded,
                StateHash = "right-hash",
                ProviderProfileId = "scripted-test",
                ScriptedPlanJson = "[]",
                Kind = (int)RunKind.Practice,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            database.DecisionRecords.AddRange(
                new DecisionRecordEntity { RunId = "run-test", Turn = 1, AgentId = "agent-a", OperationId = "left-op", ActionId = "move-left", DecisionJson = "{}" },
                new DecisionRecordEntity { RunId = "run-right", Turn = 1, AgentId = "agent-a", OperationId = "right-op", ActionId = "move-right", DecisionJson = "{}" });
            await database.SaveChangesAsync();
        }

        var comparison = await repository.GetComparisonAsync(
            "guest-owner", new RunId("run-test"), new RunId("run-right"), default);

        Assert.NotNull(comparison);
        Assert.Equal(1, comparison.FirstDifferingDecision?.Turn);
        Assert.Equal("agent-a", comparison.FirstDifferingDecision?.AgentId.Value);
        Assert.Equal("move-left", comparison.FirstDifferingDecision?.LeftActionId);
        Assert.Equal("move-right", comparison.FirstDifferingDecision?.RightActionId);
        Assert.Equal("agent-a", Assert.Single(comparison.Build.RoleOrdersChanged).Value);
    }

    [Fact]
    public async Task ReservationIsDurableIdempotentAndRejectsProjectedSpendBeforeDispatch()
    {
        var prepared = await SeedRunAsync();
        var now = ParseTime("2026-08-09T12:00:00Z");
        await repository.EnqueueTurnAsync("guest-owner", prepared.RunId, "budget-case", now, default);
        var operation = await repository.ClaimNextTurnAsync(now, TimeSpan.FromMinutes(1), default);
        Assert.NotNull(operation);
        var service = new PersistentUsageReservationService(contextFactory, TimeProvider.System);
        var profile = CreateProfile();

        var attemptCapped = profile with { RunAttemptCap = 3 };
        var attemptException = await Assert.ThrowsAsync<DirectiveDrift.Application.BudgetExceededException>(
            () => service.ReserveAsync(
                operation.OwnerId,
                operation.RunId,
                operation.OperationId,
                attemptCapped,
                2,
                default));
        Assert.Equal("run-attempt-cap", attemptException.Code);

        var first = await service.ReserveAsync(
            operation.OwnerId,
            operation.RunId,
            operation.OperationId,
            profile,
            2,
            default);
        var replay = await service.ReserveAsync(
            operation.OwnerId,
            operation.RunId,
            operation.OperationId,
            profile,
            2,
            default);

        Assert.Equal(first, replay);
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Equal(1, await database.UsageLedger.CountAsync());
        Assert.Equal("reserved", (await database.UsageLedger.SingleAsync()).Status);

        var rejected = profile with { TurnOperationCostCapMicros = first.ReservedCostMicros - 1 };
        await Assert.ThrowsAsync<DirectiveDrift.Application.BudgetExceededException>(
            () => service.ReserveAsync(
                operation.OwnerId,
                operation.RunId,
                "different-operation",
                rejected,
                2,
                default));
    }

    [Fact]
    public async Task ValidProviderCheckpointSurvivesRestartAndRequiresMatchingIntegrityFields()
    {
        var prepared = await SeedRunAsync();
        var now = ParseTime("2026-08-09T12:00:00Z");
        await repository.EnqueueTurnAsync("guest-owner", prepared.RunId, "checkpoint", now, default);
        var operation = await repository.ClaimNextTurnAsync(now, TimeSpan.FromMinutes(1), default);
        Assert.NotNull(operation);
        var agentId = operation.PreTurnState.Agents[0].AgentId;
        var result = new ProviderDecisionResult(
            new ProposedDecision(new ActionId("wait"), null, "valid", string.Empty),
            ProviderAttemptStatus.Accepted,
            new ProviderUsage(100, 10, 20, false),
            12,
            "safe-request-id",
            "prices-v1",
            "prompt-hash",
            "context-hash",
            "accepted",
            false,
            1,
            "{\"private\":true}");

        await repository.SaveAsync(
            operation.OperationId,
            agentId,
            "profile-v1",
            "state-hash-v1",
            result,
            now,
            default);
        var restarted = new EfGameRepository(contextFactory, TimeProvider.System);

        Assert.Equal(
            result,
            await restarted.GetAcceptedAsync(
                operation.OperationId,
                agentId,
                "profile-v1",
                "state-hash-v1",
                default));
        Assert.Null(await restarted.GetAcceptedAsync(
            operation.OperationId,
            agentId,
            "profile-v2",
            "state-hash-v1",
            default));
        Assert.Null(await restarted.GetAcceptedAsync(
            operation.OperationId,
            agentId,
            "profile-v1",
            "different-state",
            default));
    }

    private async Task<PreparedRun> SeedRunAsync()
    {
        const string owner = "guest-owner";
        await repository.EnsureGuestAsync(owner, default);
        await repository.CreateBuildAsync(
            owner,
            "build-test",
            "mission-test",
            "Test build",
            "{\"immutable\":true}",
            ParseTime("2026-08-09T10:00:00Z"),
            default);
        var definition = CreateDefinition();
        var runId = new RunId("run-test");
        var start = RunStartFactory.Create(runId, definition, 123, 456);
        var actions = definition.Agents.ToImmutableDictionary(
            agent => agent.AgentId,
            _ => new ActionId("wait"));
        var prepared = new PreparedRun(
            runId,
            "build-test",
            1,
            start.State,
            start.Event,
            ImmutableDictionary<int, ImmutableDictionary<AgentId, ActionId>>.Empty.Add(1, actions),
            "scripted-test");
        await repository.CreateRunAsync(
            owner,
            prepared,
            ParseTime("2026-08-09T10:01:00Z"),
            default);
        return prepared;
    }

    private static PreparedRun PrepareCertificationRun(int slot, string certificationId)
    {
        var definition = CreateDefinition() with
        {
            Mission = new MissionIdentity(
                new MissionId("mission-test"), new VariantId($"cs-cert-{slot}"),
                "content-v1", "rules-v1", "score-v1"),
        };
        var runId = new RunId($"cert-run-{slot}");
        var start = RunStartFactory.Create(runId, definition, 123, 456);
        var actions = definition.Agents.ToImmutableDictionary(
            agent => agent.AgentId, _ => new ActionId("wait"));
        return new PreparedRun(
            runId, "build-test", 1, start.State, start.Event,
            ImmutableDictionary<int, ImmutableDictionary<AgentId, ActionId>>.Empty.Add(1, actions),
            "profile-v1", RunKind.Certification, certificationId,
            $"{{\"variantId\":\"cs-cert-{slot}\"}}");
    }

    private static RunDefinition CreateDefinition()
    {
        var roomA = new RoomId("room-a");
        var roomB = new RoomId("room-b");
        return new RunDefinition(
            new MissionIdentity(
                new MissionId("mission-test"),
                new VariantId("variant-test"),
                "1",
                "1",
                "1"),
            new RunRules(3, 1, 1, 80, 80, 160),
            [roomA, roomB],
            [
                new AgentDefinition(
                    new AgentId("agent-a"),
                    AgentArchetype.Recon,
                    AgentCapabilities.Move,
                    3,
                    roomA,
                    SupportModule.None),
                new AgentDefinition(
                    new AgentId("agent-b"),
                    AgentArchetype.Engineer,
                    AgentCapabilities.Move | AgentCapabilities.RepairMajorSystem,
                    3,
                    roomB,
                    SupportModule.None),
            ],
            [new ConnectionDefinition(new ConnectionId("connection-a"), roomA, roomB, ConnectionAccess.Open, false)],
            new GeneratorDefinition(new DeviceId("generator"), roomB),
            new ConsoleDefinition(new DeviceId("console-a"), roomA, ConsoleCondition.Operational),
            new ConsoleDefinition(new DeviceId("console-b"), roomB, ConsoleCondition.Operational),
            new RecorderDefinition(new MissionItemId("recorder"), roomB),
            roomA,
            new DroneDefinition(new EntityId("drone"), [roomB], 0));
    }

    private static DateTimeOffset ParseTime(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static ProviderProfile CreateProfile() => new(
        "test-profile",
        ProviderMode.Fake,
        "test-model",
        "prompt-v1",
        2_200,
        180,
        8_192,
        TimeSpan.FromSeconds(25),
        1,
        250_000,
        2_000_000,
        10_000,
        250_000,
        500_000,
        10_000_000,
        4,
        "prices-v1");

    private sealed class TestContextFactory(DbContextOptions<GameDbContext> options)
        : IDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext() => new(options);

        public Task<GameDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
