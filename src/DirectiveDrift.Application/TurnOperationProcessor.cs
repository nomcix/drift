using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Application;

public sealed class TurnOperationProcessor(
    IGameRepository repository,
    IAgentDecisionProvider provider,
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
                operation.OperationId,
                cancellationToken);
            var requests = operation.PreTurnState.Agents
                .Where(agent => agent.Status == AgentStatus.Active)
                .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
                .Select(agent => new AgentDecisionRequest(
                    operation.OperationId,
                    operation.Turn,
                    agent.AgentId,
                    LegalActionGenerator.GetLegalActions(operation.PreTurnState, agent.AgentId),
                    operation.ScriptedActions[agent.AgentId],
                    agent.Memory))
                .ToArray();
            var decisions = await Task.WhenAll(
                requests.Select(request => provider.DecideAsync(request, cancellationToken)));
            var proposed = requests
                .Select((request, index) => (request.AgentId, Decision: decisions[index]))
                .ToDictionary(item => item.AgentId, item => item.Decision);
            var result = TurnResolver.ResolveTurn(operation.PreTurnState, proposed);

            await repository.CompleteTurnAsync(
                operation,
                result,
                usage.SettleScripted(reservation),
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
}
