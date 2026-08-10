using DirectiveDrift.Application.Models;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Application.Ports;

public interface IUsageReservationService
{
    Task<UsageReservation> ReserveAsync(
        string ownerId,
        RunId runId,
        string operationId,
        ProviderProfile profile,
        int agentCount,
        CancellationToken cancellationToken);

    UsageSettlement Settle(
        UsageReservation reservation,
        IReadOnlyCollection<ProviderDecisionResult> results);
}
