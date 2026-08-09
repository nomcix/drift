using DirectiveDrift.Application.Models;

namespace DirectiveDrift.Application.Ports;

public interface IUsageReservationService
{
    Task<UsageReservation> ReserveAsync(
        string ownerId,
        string operationId,
        CancellationToken cancellationToken);

    UsageSettlement SettleScripted(UsageReservation reservation);
}
