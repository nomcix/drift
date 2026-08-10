using System.Collections.Immutable;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Observations;
using DirectiveDrift.Core.Serialization;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Application.Tests;

public sealed class P8TurnOperationProcessorTests
{
    [Fact]
    public async Task BothProviderCallsUseSamePreStateAndStartAfterReservation()
    {
        var operation = CreateOperation();
        var repository = new OperationRepository(operation);
        var checkpoints = new CheckpointStore();
        var usage = new UsageService();
        var provider = new ConcurrentProvider(usage);
        var processor = new TurnOperationProcessor(
            repository,
            provider,
            new ContextFactory(),
            checkpoints,
            usage,
            TimeProvider.System);

        Assert.True(await processor.ProcessNextAsync(default));

        Assert.True(usage.Reserved);
        Assert.Equal(2, provider.CallCount);
        Assert.Equal(2, provider.MaximumConcurrency);
        Assert.Single(provider.PreStateHashes.Distinct(StringComparer.Ordinal));
        Assert.NotNull(repository.CompletedResult);
        Assert.Equal(2, checkpoints.SavedCount);
    }

    [Fact]
    public async Task RestartReusesStoredFinalDecisionsWithoutCallingProviderAgain()
    {
        var operation = CreateOperation();
        var repository = new OperationRepository(operation);
        var usage = new UsageService();
        var provider = new ConcurrentProvider(usage, throwIfCalled: true);
        var stored = operation.PreTurnState.Agents.ToDictionary(
            value => value.AgentId,
            value => CreateResult(value.Memory));
        var checkpoints = new CheckpointStore(stored);
        var processor = new TurnOperationProcessor(
            repository,
            provider,
            new ContextFactory(),
            checkpoints,
            usage,
            TimeProvider.System);

        Assert.True(await processor.ProcessNextAsync(default));

        Assert.Equal(0, provider.CallCount);
        Assert.NotNull(repository.CompletedResult);
        Assert.Equal(0, checkpoints.SavedCount);
    }

    private static ClaimedTurnOperation CreateOperation()
    {
        var roomA = new RoomId("room-a");
        var roomB = new RoomId("room-b");
        var definition = new RunDefinition(
            new MissionIdentity(new MissionId("mission"), new VariantId("variant"), "1", "1", "1"),
            new RunRules(3, 1, 1, 80, 80, 160),
            [roomA, roomB],
            [
                new AgentDefinition(new AgentId("agent-a"), AgentArchetype.Recon, AgentCapabilities.Move, 3, roomA, SupportModule.None),
                new AgentDefinition(new AgentId("agent-b"), AgentArchetype.Engineer, AgentCapabilities.Move, 3, roomB, SupportModule.None),
            ],
            [new ConnectionDefinition(new ConnectionId("link"), roomA, roomB, ConnectionAccess.Open, false)],
            new GeneratorDefinition(new DeviceId("generator"), roomB),
            new ConsoleDefinition(new DeviceId("console-a"), roomA, ConsoleCondition.Operational),
            new ConsoleDefinition(new DeviceId("console-b"), roomB, ConsoleCondition.Operational),
            new RecorderDefinition(new MissionItemId("recorder"), roomB),
            roomA,
            new DroneDefinition(new EntityId("drone"), [roomB], 0));
        var start = RunStartFactory.Create(new RunId("run-test"), definition, 123, 456);
        var actions = definition.Agents.ToImmutableDictionary(
            value => value.AgentId,
            _ => new ActionId("wait"));
        return new ClaimedTurnOperation(
            "operation-test",
            "lease-test",
            "guest-test",
            start.State.RunId,
            1,
            start.State,
            actions,
            "{}",
            TestProfile.ProfileId);
    }

    private static ProviderDecisionResult CreateResult(string memory) => new(
        new ProposedDecision(new ActionId("wait"), null, "valid", memory),
        ProviderAttemptStatus.Accepted,
        new ProviderUsage(1, 1, 0, false),
        1,
        null,
        TestProfile.PriceTableVersion,
        "prompt-hash",
        "context-hash",
        "accepted",
        false,
        1,
        "{}");

    private static ProviderProfile TestProfile { get; } = new(
        "test-profile", ProviderMode.Fake, "fake", "prompt-v1", 2200, 180, 8192,
        TimeSpan.FromSeconds(1), 1, 0, 0, 0, 0, 0, 0, 4, "prices-v1");

    private sealed class ContextFactory : IAgentTurnContextFactory
    {
        public AgentTurnContext Create(
            RunState preTurnState,
            string canonicalBuildJson,
            AgentId agentId,
            ProviderProfile profile)
        {
            var observation = PrivateObservationBuilder.Build(preTurnState, agentId);
            return new AgentTurnContext(
                "v1", preTurnState.RunId, 1, new AgentIdentityView(agentId, agentId.Value),
                "rules", string.Empty, string.Empty, [], new AgentCapabilityView([]), null,
                observation, [], observation.Self.Memory,
                observation.LegalActions.Actions.Select(value => new LegalActionView(value.ActionId, value.Kind.ToString(), null)).ToArray(),
                new RuntimeLimits(80, 160, 180, 180, 8192));
        }
    }

    private sealed class ConcurrentProvider(UsageService usage, bool throwIfCalled = false)
        : IAgentDecisionProvider
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int active;
        private int calls;
        private int maximumConcurrency;

        public string ProfileId => TestProfile.ProfileId;
        public ProviderProfile Profile => TestProfile;
        public int CallCount => calls;
        public int MaximumConcurrency => maximumConcurrency;
        public List<string> PreStateHashes { get; } = [];

        public async Task<ProviderDecisionResult> DecideAsync(
            AgentDecisionRequest request,
            CancellationToken cancellationToken)
        {
            if (throwIfCalled)
            {
                throw new InvalidOperationException("Stored result should have been reused.");
            }

            Assert.True(usage.Reserved);
            Interlocked.Increment(ref calls);
            var current = Interlocked.Increment(ref active);
            maximumConcurrency = Math.Max(maximumConcurrency, current);
            lock (PreStateHashes)
            {
                PreStateHashes.Add(request.Context.Observation.PreDecisionStateHash);
            }
            if (Volatile.Read(ref calls) == 2)
            {
                release.SetResult();
            }

            await release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref active);
            return CreateResult(request.CurrentMemory);
        }
    }

    private sealed class UsageService : IUsageReservationService
    {
        public bool Reserved { get; private set; }

        public Task<UsageReservation> ReserveAsync(
            string ownerId,
            RunId runId,
            string operationId,
            ProviderProfile profile,
            int agentCount,
            CancellationToken cancellationToken)
        {
            Reserved = true;
            return Task.FromResult(new UsageReservation("reservation", operationId, 0, 0, 0));
        }

        public UsageSettlement Settle(
            UsageReservation reservation,
            IReadOnlyCollection<ProviderDecisionResult> results) =>
            new(reservation.ReservationId, 0, 0, 0);
    }

    private sealed class CheckpointStore(
        IReadOnlyDictionary<AgentId, ProviderDecisionResult>? stored = null)
        : IDecisionCheckpointStore
    {
        public int SavedCount { get; private set; }

        public Task<ProviderDecisionResult?> GetAcceptedAsync(
            string operationId,
            AgentId agentId,
            string providerProfileId,
            string preDecisionStateHash,
            CancellationToken cancellationToken) =>
            Task.FromResult(stored?.GetValueOrDefault(agentId));

        public Task SaveAsync(
            string operationId,
            AgentId agentId,
            string providerProfileId,
            string preDecisionStateHash,
            ProviderDecisionResult result,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            SavedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class OperationRepository(ClaimedTurnOperation operation) : IGameRepository
    {
        private bool claimed;
        public TurnResult? CompletedResult { get; private set; }

        public Task<ClaimedTurnOperation?> ClaimNextTurnAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken)
        {
            if (claimed) return Task.FromResult<ClaimedTurnOperation?>(null);
            claimed = true;
            return Task.FromResult<ClaimedTurnOperation?>(operation);
        }

        public Task CompleteTurnAsync(ClaimedTurnOperation value, TurnResult result, UsageSettlement settlement, DateTimeOffset now, CancellationToken cancellationToken)
        { CompletedResult = result; return Task.CompletedTask; }

        public Task FailTurnAsync(ClaimedTurnOperation value, string errorCode, DateTimeOffset now, CancellationToken cancellationToken) => throw new InvalidOperationException(errorCode);
        public Task<bool> GuestExistsAsync(string ownerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task EnsureGuestAsync(string ownerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BuildVersionSnapshot> CreateBuildAsync(string ownerId, string buildId, string missionId, string name, string canonicalJson, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ImmutableArray<BuildSummary>> ListBuildsAsync(string ownerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BuildVersionSnapshot?> GetBuildVersionAsync(string ownerId, string buildId, int version, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ImmutableArray<BuildVersionSnapshot>> ListBuildVersionsAsync(string ownerId, string buildId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BuildVersionSnapshot?> AddBuildVersionAsync(string ownerId, string buildId, string canonicalJson, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RunSummary> CreateRunAsync(string ownerId, PreparedRun preparedRun, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RunSummary?> GetRunAsync(string ownerId, RunId runId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EnqueueTurnResult?> EnqueueTurnAsync(string ownerId, RunId runId, string idempotencyKey, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TurnOperationSummary?> GetOperationAsync(string ownerId, string operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ImmutableArray<CanonicalEvent>?> GetEventsAsync(string ownerId, RunId runId, long afterSequence, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReplayData?> GetReplayAsync(string ownerId, RunId runId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
