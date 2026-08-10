using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.AI;

public sealed class ScriptedUsageReservationService : IUsageReservationService
{
    public Task<UsageReservation> ReserveAsync(
        string ownerId,
        RunId runId,
        string operationId,
        ProviderProfile profile,
        int agentCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new UsageReservation(operationId, operationId, 0, 0, 0));
    }

    public UsageSettlement Settle(
        UsageReservation reservation,
        IReadOnlyCollection<ProviderDecisionResult> results) =>
        new(reservation.ReservationId, 0, 0, 0);
}
