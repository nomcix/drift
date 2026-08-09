using System.Collections.Immutable;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Application.Ports;

public interface IGameRepository
{
    Task<bool> GuestExistsAsync(string ownerId, CancellationToken cancellationToken);

    Task EnsureGuestAsync(string ownerId, CancellationToken cancellationToken);

    Task<BuildVersionSnapshot> CreateBuildAsync(
        string ownerId,
        string buildId,
        string missionId,
        string name,
        string canonicalJson,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ImmutableArray<BuildSummary>> ListBuildsAsync(
        string ownerId,
        CancellationToken cancellationToken);

    Task<BuildVersionSnapshot?> GetBuildVersionAsync(
        string ownerId,
        string buildId,
        int version,
        CancellationToken cancellationToken);

    Task<ImmutableArray<BuildVersionSnapshot>> ListBuildVersionsAsync(
        string ownerId,
        string buildId,
        CancellationToken cancellationToken);

    Task<BuildVersionSnapshot?> AddBuildVersionAsync(
        string ownerId,
        string buildId,
        string canonicalJson,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<RunSummary> CreateRunAsync(
        string ownerId,
        PreparedRun preparedRun,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<RunSummary?> GetRunAsync(
        string ownerId,
        RunId runId,
        CancellationToken cancellationToken);

    Task<EnqueueTurnResult?> EnqueueTurnAsync(
        string ownerId,
        RunId runId,
        string idempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<TurnOperationSummary?> GetOperationAsync(
        string ownerId,
        string operationId,
        CancellationToken cancellationToken);

    Task<ClaimedTurnOperation?> ClaimNextTurnAsync(
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task CompleteTurnAsync(
        ClaimedTurnOperation operation,
        TurnResult result,
        UsageSettlement settlement,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task FailTurnAsync(
        ClaimedTurnOperation operation,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ImmutableArray<CanonicalEvent>?> GetEventsAsync(
        string ownerId,
        RunId runId,
        long afterSequence,
        int limit,
        CancellationToken cancellationToken);

    Task<ReplayData?> GetReplayAsync(
        string ownerId,
        RunId runId,
        CancellationToken cancellationToken);
}
