using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Application;

public sealed class TurnOperationProcessor(
    IGameRepository repository,
    IAgentDecisionProvider provider,
    IAgentTurnContextFactory contextFactory,
    IDecisionCheckpointStore checkpoints,
    IUsageReservationService usage,
    TimeProvider timeProvider)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var operation = await repository.ClaimNextTurnAsync(
            timeProvider.GetUtcNow(),
            TimeSpan.FromSeconds(30),
            cancellationToken);
        if (operation is null)
        {
            return false;
        }

        try
        {
            var reservation = await usage.ReserveAsync(
                operation.OwnerId,
                operation.RunId,
                operation.OperationId,
                provider.Profile,
                operation.PreTurnState.Agents.Count(agent => agent.Status == AgentStatus.Active),
                cancellationToken);
            var activeAgents = operation.PreTurnState.Agents
                .Where(agent => agent.Status == AgentStatus.Active)
                .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
                .ToArray();
            var requests = activeAgents
                .Select(agent => new AgentDecisionRequest(
                    operation.OperationId,
                    operation.Turn,
                    agent.AgentId,
                    LegalActionGenerator.GetLegalActions(operation.PreTurnState, agent.AgentId),
                    operation.ScriptedActions[agent.AgentId],
                    agent.Memory,
                    activeAgents.Single(other => other.AgentId != agent.AgentId).AgentId,
                    contextFactory.Create(
                        operation.PreTurnState,
                        operation.CanonicalBuildJson,
                        agent.AgentId,
                        provider.Profile)))
                .ToArray();
            var providerResults = await Task.WhenAll(
                requests.Select(request => DecideWithCheckpointAsync(
                    operation,
                    request,
                    cancellationToken)));
            var proposed = requests
                .Select((request, index) => (request.AgentId, providerResults[index].Decision))
                .ToDictionary(item => item.AgentId, item => item.Decision);
            var result = TurnResolver.ResolveTurn(operation.PreTurnState, proposed);

            await repository.CompleteTurnAsync(
                operation,
                result,
                usage.Settle(reservation, providerResults),
                timeProvider.GetUtcNow(),
                cancellationToken);
            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await repository.FailTurnAsync(
                operation,
                "turn-processing-failed",
                timeProvider.GetUtcNow(),
                cancellationToken);
            return true;
        }
    }

    private async Task<ProviderDecisionResult> DecideWithCheckpointAsync(
        ClaimedTurnOperation operation,
        AgentDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var preDecisionStateHash = request.Context.Observation.PreDecisionStateHash;
        var stored = await checkpoints.GetAcceptedAsync(
            operation.OperationId,
            request.AgentId,
            provider.ProfileId,
            preDecisionStateHash,
            cancellationToken);
        if (stored is not null)
        {
            return stored;
        }

        var result = await provider.DecideAsync(request, cancellationToken);
        await checkpoints.SaveAsync(
            operation.OperationId,
            request.AgentId,
            provider.ProfileId,
            preDecisionStateHash,
            result,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return result;
    }
}
