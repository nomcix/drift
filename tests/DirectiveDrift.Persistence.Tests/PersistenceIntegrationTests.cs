using System.Collections.Immutable;
using System.Globalization;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;
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

        await repository.CompleteTurnAsync(
            operation,
            result,
            new UsageSettlement(operation.OperationId, 0, 0, 0),
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

    private sealed class TestContextFactory(DbContextOptions<GameDbContext> options)
        : IDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext() => new(options);

        public Task<GameDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
