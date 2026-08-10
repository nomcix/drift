using DirectiveDrift.Application.Models;
using DirectiveDrift.Application.Ports;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Observations;

namespace DirectiveDrift.Api;

public sealed class AgentTurnContextFactory(ValidatedMission mission) : IAgentTurnContextFactory
{
    public const string ContextVersion = "agent-turn-context-v1";

    public AgentTurnContext Create(
        RunState preTurnState,
        string canonicalBuildJson,
        AgentId agentId,
        ProviderProfile profile)
    {
        var build = ContractJson.Deserialize<BuildDocument>(canonicalBuildJson);
        var configured = build.Agents[agentId];
        var authoredAgent = mission.Agents[agentId];
        var stateAgent = preTurnState.Agents.Single(value => value.AgentId == agentId);
        var observation = PrivateObservationBuilder.Build(preTurnState, agentId);
        var module = mission.Modules.Values.SingleOrDefault(
            value => string.Equals(value.ModuleId, configured.ModuleId, StringComparison.Ordinal));
        var memoryLimit = stateAgent.Module.Module == SupportModule.MemoryBuffer
            ? preTurnState.Rules.MemoryBufferLength
            : preTurnState.Rules.BaseMemoryLength;

        return new AgentTurnContext(
            ContextVersion,
            preTurnState.RunId,
            preTurnState.Turn + 1,
            new AgentIdentityView(agentId, authoredAgent.Label),
            "Choose exactly one listed legal action. A message may accompany any action; there is no separate message action. Messages arrive on a later turn. Only observed or delivered facts are known.",
            build.SharedDoctrine,
            configured.RoleOrder,
            configured.BriefingCardIds.Select(
                cardId =>
                {
                    var card = mission.BriefingCards[new BriefingCardId(cardId)];
                    return new BriefingCardView(card.CardId, card.Title, card.Text);
                }).ToArray(),
            new AgentCapabilityView(authoredAgent.Capabilities.ToArray()),
            module is null
                ? null
                : new ModuleView(module.ModuleId, module.Label, module.Description),
            observation,
            observation.DeliveredMessages.Select(
                message => new DeliveredMessageView(
                    message.MessageId.Value,
                    message.SenderAgentId,
                    message.SentTurn,
                    message.DeliveryTurn,
                    message.Text)).ToArray(),
            stateAgent.Memory,
            observation.LegalActions.Actions.Select(
                action => new LegalActionView(
                    action.ActionId,
                    action.Kind.ToString(),
                    action.Target.Kind == RuleTargetKind.None ? null : action.Target.Value)).ToArray(),
            new RuntimeLimits(
                preTurnState.Rules.MaxMessageLength,
                memoryLimit,
                180,
                profile.MaximumOutputTokens,
                profile.MaximumResponseBytes));
    }
}
