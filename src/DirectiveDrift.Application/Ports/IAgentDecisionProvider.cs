using DirectiveDrift.Application.Models;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Application.Ports;

public sealed record AgentDecisionRequest(
    string OperationId,
    int Turn,
    AgentId AgentId,
    LegalActionSet LegalActions,
    ActionId ScriptedActionId,
    string CurrentMemory,
    AgentId OtherAgentId,
    AgentTurnContext Context);

public interface IAgentDecisionProvider
{
    string ProfileId { get; }

    ProviderProfile Profile { get; }

    Task<ProviderDecisionResult> DecideAsync(
        AgentDecisionRequest request,
        CancellationToken cancellationToken);
}

public interface IAgentTurnContextFactory
{
    AgentTurnContext Create(
        RunState preTurnState,
        string canonicalBuildJson,
        AgentId agentId,
        ProviderProfile profile);
}
