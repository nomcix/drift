using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;

namespace DirectiveDrift.AI;

public sealed class ScriptedUsageReservationService : IUsageReservationService
{
    public Task<UsageReservation> ReserveAsync(
        string ownerId,
        string operationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new UsageReservation(operationId, operationId, 0, 0, 0));
    }

    public UsageSettlement SettleScripted(UsageReservation reservation) =>
        new(reservation.ReservationId, 0, 0, 0);
}
