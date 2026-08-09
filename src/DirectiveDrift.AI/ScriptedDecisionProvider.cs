using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Decisions;

namespace DirectiveDrift.AI;

public sealed class ScriptedDecisionProvider : IAgentDecisionProvider
{
    public string ProfileId => "scripted-reference-v1";

    public Task<ProposedDecision> DecideAsync(
        AgentDecisionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.LegalActions.Find(request.ScriptedActionId) is null)
        {
            throw new InvalidOperationException(
                $"Scripted action '{request.ScriptedActionId}' is not legal for the current turn.");
        }

        return Task.FromResult(
            new ProposedDecision(
                request.ScriptedActionId,
                null,
                "scripted-reference",
                request.CurrentMemory));
    }
}
