using DirectiveDrift.Application;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace DirectiveDrift.Persistence;

public sealed class PersistentUsageReservationService(
    IDbContextFactory<GameDbContext> contextFactory,
    TimeProvider timeProvider) : IUsageReservationService
{
    public async Task<UsageReservation> ReserveAsync(
        string ownerId,
        RunId runId,
        string operationId,
        ProviderProfile profile,
        int agentCount,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var existing = await database.UsageLedger.SingleOrDefaultAsync(
            value => value.OperationId == operationId,
            cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToReservation(existing);
        }

        var attemptCount = checked(agentCount * (profile.MaximumRepairRetries + 1));
        var reservedInput = checked(attemptCount * profile.MaximumInputTokens);
        var reservedOutput = checked(attemptCount * profile.MaximumOutputTokens);
        var projectedCost = CostMicros(
                reservedInput,
                profile.InputPriceMicrosPerMillionTokens)
            + CostMicros(reservedOutput, profile.OutputPriceMicrosPerMillionTokens);
        if (projectedCost > profile.TurnOperationCostCapMicros)
        {
            throw new BudgetExceededException("turn-operation-cost-cap");
        }

        var now = timeProvider.GetUtcNow();
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var committedEntries = await database.UsageLedger.AsNoTracking().ToArrayAsync(cancellationToken);
        var activeOperationIds = (await database.TurnOperations
                .AsNoTracking()
                .Where(value => value.Status == (int)TurnOperationStatus.Queued
                    || value.Status == (int)TurnOperationStatus.Processing)
                .Select(value => value.Id)
                .ToArrayAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var runCommitted = committedEntries
            .Where(value => value.RunId == runId.Value)
            .Sum(value => CommittedCost(value, activeOperationIds));
        var guestCommitted = committedEntries
            .Where(value => value.OwnerId == ownerId && value.CreatedAt >= dayStart)
            .Sum(value => CommittedCost(value, activeOperationIds));
        var deploymentCommitted = committedEntries
            .Where(value => value.CreatedAt >= dayStart)
            .Sum(value => CommittedCost(value, activeOperationIds));
        var concurrent = committedEntries.Count(
            value => value.Status == "reserved" && activeOperationIds.Contains(value.OperationId));
        var priorAttempts = await database.ProviderDecisionCheckpoints
            .Join(
                database.TurnOperations.Where(value => value.RunId == runId.Value),
                checkpoint => checkpoint.OperationId,
                operation => operation.Id,
                (checkpoint, _) => checkpoint.AttemptCount)
            .SumAsync(cancellationToken);

        EnsureWithin(runCommitted, projectedCost, profile.RunCostCapMicros, "run-cost-cap");
        EnsureWithin(guestCommitted, projectedCost, profile.GuestDailyCostCapMicros, "guest-daily-cost-cap");
        EnsureWithin(
            deploymentCommitted,
            projectedCost,
            profile.DeploymentDailyCostCapMicros,
            "deployment-daily-cost-cap");
        if (concurrent >= profile.ConcurrencyCap)
        {
            throw new BudgetExceededException("provider-concurrency-cap");
        }

        if (priorAttempts + attemptCount > profile.RunAttemptCap)
        {
            throw new BudgetExceededException("run-attempt-cap");
        }

        var entity = new UsageLedgerEntity
        {
            Id = $"usage:{operationId}",
            OwnerId = ownerId,
            RunId = runId.Value,
            OperationId = operationId,
            ReservedInputTokens = reservedInput,
            ReservedOutputTokens = reservedOutput,
            ReservedCostMicros = projectedCost,
            ActualInputTokens = 0,
            ActualOutputTokens = 0,
            ActualCostMicros = 0,
            Status = "reserved",
            CreatedAt = now,
            UpdatedAt = now,
        };
        database.UsageLedger.Add(entity);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToReservation(entity);
    }

    public UsageSettlement Settle(
        UsageReservation reservation,
        IReadOnlyCollection<ProviderDecisionResult> results)
    {
        var input = results.Sum(value => value.Usage.InputTokens);
        var output = results.Sum(value => value.Usage.OutputTokens);
        var cost = results.Sum(value => value.Usage.CostMicros);
        if (input < 0 || output < 0 || cost < 0 || cost > reservation.ReservedCostMicros)
        {
            throw new InvalidOperationException("Usage settlement is outside its reservation.");
        }

        return new UsageSettlement(reservation.ReservationId, input, output, cost);
    }

    private static void EnsureWithin(int committed, int projected, int cap, string code)
    {
        if ((long)committed + projected > cap)
        {
            throw new BudgetExceededException(code);
        }
    }

    private static int CostMicros(int tokens, int priceMicrosPerMillionTokens) =>
        checked((int)(((long)tokens * priceMicrosPerMillionTokens + 999_999) / 1_000_000));

    private static int CommittedCost(
        UsageLedgerEntity value,
        HashSet<string> activeOperationIds) =>
        value.Status == "reserved"
            ? activeOperationIds.Contains(value.OperationId) ? value.ReservedCostMicros : 0
            : value.ActualCostMicros;

    private static UsageReservation ToReservation(UsageLedgerEntity value) =>
        new(
            value.Id,
            value.OperationId,
            value.ReservedInputTokens,
            value.ReservedOutputTokens,
            value.ReservedCostMicros);
}
