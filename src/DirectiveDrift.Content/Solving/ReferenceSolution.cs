using System.Collections.Immutable;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Solving;

public sealed record ScriptedAgentDecision(AgentId AgentId, ActionId ActionId);

public sealed record ScriptedTurn(
    int Turn,
    ImmutableArray<ScriptedAgentDecision> Decisions);

public sealed record ReferencePolicyOptions(
    AgentId AlphaAgentId,
    AgentId RecorderCarrierAgentId,
    int MaximumTurns = 17,
    int MinimumSyncTurn = 1,
    bool RequireNoDamage = true,
    SupportModule? RequiredConsumedModule = null,
    int MaximumExpandedStates = 250_000);

public sealed record ReferenceSolution(
    bool Solved,
    RunState? FinalState,
    ImmutableArray<ScriptedTurn> Turns,
    ImmutableArray<CanonicalEvent> Events,
    int ExpandedStates,
    string? Failure)
{
    public int? CompletionTurn => FinalState?.Status == RunStatus.Succeeded
        ? FinalState.Turn
        : null;

    public int DamageTaken => FinalState is null
        ? 0
        : FinalState.Agents.Sum(agent => agent.MaxHealth - agent.Health);

    public int? SyncTurn => Events
        .Where(canonicalEvent => canonicalEvent.Type == CanonicalEventType.ArchiveOpened)
        .Select(canonicalEvent => (int?)canonicalEvent.Turn)
        .SingleOrDefault();
}
