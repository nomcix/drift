using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Decisions;

namespace DirectiveDrift.AI;

public sealed class ScriptedDecisionProvider : IAgentDecisionProvider
{
    public string ProfileId => "scripted-reference-v1";

    public ProviderProfile Profile { get; } = ProviderProfiles.Scripted;

    public Task<ProviderDecisionResult> DecideAsync(
        AgentDecisionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.LegalActions.Find(request.ScriptedActionId) is null)
        {
            throw new InvalidOperationException(
                $"Scripted action '{request.ScriptedActionId}' is not legal for the current turn.");
        }

        var decision = new ProposedDecision(
                request.ScriptedActionId,
                null,
                "scripted-reference",
                request.CurrentMemory);
        return Task.FromResult(
            new ProviderDecisionResult(
                decision,
                ProviderAttemptStatus.Accepted,
                new ProviderUsage(0, 0, 0, false),
                0,
                null,
                Profile.PriceTableVersion,
                string.Empty,
                string.Empty,
                "accepted-scripted",
                false,
                1,
                string.Empty));
    }
}
