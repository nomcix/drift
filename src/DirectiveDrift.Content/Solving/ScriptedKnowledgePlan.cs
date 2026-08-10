using System.Collections.Immutable;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Solving;

public static class ScriptedKnowledgePlan
{
    private const string SyncCardId = "sync-contract";

    public static ImmutableArray<ScriptedTurn> Apply(
        BuildDocument build,
        RunDefinition definition,
        ImmutableArray<ScriptedTurn> solvedTurns)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(definition);

        var missingSyncAgents = build.Agents
            .Where(entry => !entry.Value.BriefingCardIds.Contains(SyncCardId, StringComparer.Ordinal))
            .Select(entry => entry.Key)
            .ToHashSet();
        var blockedSyncTurn = solvedTurns
            .Where(turn => turn.Decisions.Any(decision =>
                missingSyncAgents.Contains(decision.AgentId)
                && decision.ActionId.Value.StartsWith("activate:", StringComparison.Ordinal)))
            .Select(turn => (int?)turn.Turn)
            .FirstOrDefault();
        var turns = solvedTurns
            .Select(turn => turn with
            {
                Decisions = turn.Decisions
                    .Select(decision => blockedSyncTurn is not null
                        && turn.Turn > blockedSyncTurn
                        || missingSyncAgents.Contains(decision.AgentId)
                        && decision.ActionId.Value.StartsWith("activate:", StringComparison.Ordinal)
                            ? decision with { ActionId = new ActionId("wait") }
                            : decision)
                    .ToImmutableArray(),
            })
            .ToList();
        var nextTurn = turns.Count == 0 ? 1 : turns[^1].Turn + 1;
        while (nextTurn <= definition.Rules.TurnLimit)
        {
            turns.Add(new ScriptedTurn(
                nextTurn,
                definition.Agents
                    .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
                    .Select(agent => new ScriptedAgentDecision(agent.AgentId, new ActionId("wait")))
                    .ToImmutableArray()));
            nextTurn++;
        }

        return turns.ToImmutableArray();
    }
}
