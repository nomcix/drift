using DirectiveDrift.Application.Models;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Application.Ports;

public interface IDecisionCheckpointStore
{
    Task<ProviderDecisionResult?> GetAcceptedAsync(
        string operationId,
        AgentId agentId,
        string providerProfileId,
        string preDecisionStateHash,
        CancellationToken cancellationToken);

    Task SaveAsync(
        string operationId,
        AgentId agentId,
        string providerProfileId,
        string preDecisionStateHash,
        ProviderDecisionResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
