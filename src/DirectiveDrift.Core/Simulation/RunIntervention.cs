using System.Collections.Immutable;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Serialization;

namespace DirectiveDrift.Core.Simulation;

public sealed record InterventionResult(
    RunState State,
    ImmutableArray<CanonicalEvent> Events,
    string StateHash);

public static class RunIntervention
{
    private static readonly AgentId MissionControl = new("mission-control");

    public static InterventionResult ApplyEmergencyBurst(RunState state, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (state.Status != RunStatus.Active || state.Score.Assisted)
        {
            throw new InvalidOperationException("Emergency Burst requires an active, unassisted run.");
        }

        if (text.Length is < 1 or > 120 || text.Any(char.IsControl))
        {
            throw new ArgumentException("Emergency Burst text must contain 1-120 printable characters.", nameof(text));
        }

        var deliveryTurn = state.Turn + 1;
        var messages = state.Agents
            .OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal)
            .Select(agent => new AgentMessage(
                new MessageId($"{state.RunId.Value}:burst:{agent.AgentId.Value}"),
                MissionControl,
                agent.AgentId,
                state.Turn,
                deliveryTurn,
                text))
            .ToImmutableArray();
        var updated = CanonicalStateSerializer.Normalize(state with
        {
            Communication = state.Communication with
            {
                RemainingMessages = 0,
                QueuedMessages = state.Communication.QueuedMessages.AddRange(messages),
            },
            Score = state.Score with { Assisted = true },
            NextEventSequence = state.NextEventSequence + messages.Length,
        });
        var hash = CanonicalStateSerializer.Hash(updated);
        var events = messages.Select((message, index) => new CanonicalEvent(
            new EventId($"{state.RunId.Value}:{state.NextEventSequence + index}"),
            state.NextEventSequence + index,
            state.Turn,
            TurnPhase.Communicate,
            CanonicalEventType.MessageQueued,
            new MessagePayload(
                message.MessageId,
                message.SenderAgentId,
                message.RecipientAgentId,
                message.SentTurn,
                message.DeliveryTurn,
                message.Text,
                null),
            "1",
            index == messages.Length - 1 ? hash : null)).ToImmutableArray();
        return new InterventionResult(updated, events, hash);
    }
}
