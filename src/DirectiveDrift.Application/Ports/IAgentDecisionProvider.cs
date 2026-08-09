using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Application.Ports;

public sealed record AgentDecisionRequest(
    string OperationId,
    int Turn,
    AgentId AgentId,
    LegalActionSet LegalActions,
    ActionId ScriptedActionId,
    string CurrentMemory);

public interface IAgentDecisionProvider
{
    string ProfileId { get; }

    Task<ProposedDecision> DecideAsync(
        AgentDecisionRequest request,
        CancellationToken cancellationToken);
}
