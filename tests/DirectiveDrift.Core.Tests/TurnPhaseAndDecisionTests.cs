using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Core.Tests;

public sealed class TurnPhaseAndDecisionTests
{
    [Fact]
    public void MessageQueuesBeforeMovementAndDeliversBeforeNextObservation()
    {
        var first = TurnResolver.ResolveTurn(
            CoreScenario.Start(),
            CoreScenario.Decisions(
                CoreScenario.Decision("wait", "take generator"),
                CoreScenario.Decision("move:generator-room")));

        Assert.All(first.Observations, observation => Assert.Empty(observation.DeliveredMessages));
        Assert.True(
            EventIndex(first, CanonicalEventType.MessageQueued)
            < EventIndex(first, CanonicalEventType.AgentMoved));

        var second = TurnResolver.ResolveTurn(
            first.State,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));
        var wrenObservation = second.Observations.Single(
            observation => observation.AgentId == CoreScenario.WrenId);

        Assert.Equal("take generator", Assert.Single(wrenObservation.DeliveredMessages).Text);
        Assert.True(
            EventIndex(second, CanonicalEventType.MessageDelivered)
            < EventIndex(second, CanonicalEventType.AgentDecisionAccepted));
    }

    [Fact]
    public void BothDecisionsUseTheSamePreDecisionStateVersion()
    {
        var result = TurnResolver.ResolveTurn(
            CoreScenario.Start(),
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));

        Assert.Equal(2, result.Observations.Length);
        Assert.Single(result.Observations.Select(observation => observation.PreDecisionStateHash).Distinct());
    }

    [Fact]
    public void AllMovementEventsPrecedeAllInteractionEvents()
    {
        var result = TurnResolver.ResolveTurn(
            CoreScenario.Start(),
            CoreScenario.Decisions(
                CoreScenario.Decision("scan:engineer-start"),
                CoreScenario.Decision("move:generator-room")));

        Assert.True(
            EventIndex(result, CanonicalEventType.AgentMoved)
            < EventIndex(result, CanonicalEventType.RoomScanned));
    }

    [Fact]
    public void IllegalActionFallsBackToWaitAndDiscardsMessageAndMemory()
    {
        var state = CoreScenario.UpdateAgent(
            CoreScenario.Start(),
            CoreScenario.KiteId,
            agent => agent with { Memory = "keep" });

        var result = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("move:hidden", "discard", "replace"),
                CoreScenario.Decision("wait")));
        var decision = result.Decisions.Single(item => item.AgentId == CoreScenario.KiteId);

        Assert.True(decision.UsedFallback);
        Assert.Equal(DecisionFallbackReason.IllegalAction, decision.FallbackReason);
        Assert.Equal("wait", decision.Action.ActionId.Value);
        Assert.Null(decision.Message);
        Assert.Equal(
            "keep",
            result.State.Agents.Single(agent => agent.AgentId == CoreScenario.KiteId).Memory);
    }

    [Theory]
    [InlineData(DecisionFallbackReason.MessageTooLong)]
    [InlineData(DecisionFallbackReason.RationaleTooLong)]
    [InlineData(DecisionFallbackReason.MemoryTooLong)]
    public void TextLimitViolationsUseDeterministicFallback(DecisionFallbackReason expected)
    {
        var proposal = expected switch
        {
            DecisionFallbackReason.MessageTooLong =>
                new ProposedDecision(new ActionId("wait"), new string('m', 121), "ok", ""),
            DecisionFallbackReason.RationaleTooLong =>
                new ProposedDecision(new ActionId("wait"), null, new string('r', 181), ""),
            DecisionFallbackReason.MemoryTooLong =>
                new ProposedDecision(new ActionId("wait"), null, "ok", new string('x', 241)),
            _ => throw new ArgumentOutOfRangeException(nameof(expected)),
        };

        var result = TurnResolver.ResolveTurn(
            CoreScenario.Start(),
            CoreScenario.Decisions(proposal, CoreScenario.Decision("wait")));

        Assert.Equal(expected, result.Decisions[0].FallbackReason);
    }

    [Fact]
    public void MemoryBufferRaisesOnlyItsOwnersMemoryLimit()
    {
        var result = TurnResolver.ResolveTurn(
            CoreScenario.Start(kiteModule: SupportModule.MemoryBuffer),
            CoreScenario.Decisions(
                new ProposedDecision(new ActionId("wait"), null, "ok", new string('x', 400)),
                new ProposedDecision(new ActionId("wait"), null, "ok", new string('x', 241))));

        Assert.Null(result.Decisions.Single(item => item.AgentId == CoreScenario.KiteId).FallbackReason);
        Assert.Equal(
            DecisionFallbackReason.MemoryTooLong,
            result.Decisions.Single(item => item.AgentId == CoreScenario.WrenId).FallbackReason);
    }

    [Fact]
    public void SharedMessageContentionUsesStableAgentIdOrder()
    {
        var state = CoreScenario.Start() with
        {
            Communication = CoreScenario.Start().Communication with { RemainingMessages = 1 },
        };

        var result = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("wait", "kite"),
                CoreScenario.Decision("wait", "wren"),
                reverseInsertion: true));

        Assert.Equal(0, result.State.Communication.RemainingMessages);
        Assert.Equal(CoreScenario.KiteId, Assert.Single(result.State.Communication.QueuedMessages)
            .SenderAgentId);
        Assert.Contains(result.Events, item => item.Type == CanonicalEventType.MessageRejected);
    }

    [Fact]
    public void OpposingMovesResolveWithoutCollisionInStableAgentOrder()
    {
        var result = TurnResolver.ResolveTurn(
            CoreScenario.Start(),
            CoreScenario.Decisions(
                CoreScenario.Decision("move:engineer-start"),
                CoreScenario.Decision("move:extraction"),
                reverseInsertion: true));
        var moves = result.Events.Where(item => item.Type == CanonicalEventType.AgentMoved).ToArray();

        Assert.Equal(2, moves.Length);
        Assert.Equal(CoreScenario.KiteId, Assert.IsType<AgentMovedPayload>(moves[0].Payload).AgentId);
        Assert.Equal(CoreScenario.WrenId, Assert.IsType<AgentMovedPayload>(moves[1].Payload).AgentId);
    }

    [Fact]
    public void DroneCollisionDamageUsesStableAgentIdOrder()
    {
        var state = CoreScenario.Start();
        state = CoreScenario.PlaceAgent(state, CoreScenario.KiteId, CoreScenario.BetaRoom);
        state = CoreScenario.PlaceAgent(state, CoreScenario.WrenId, CoreScenario.BetaRoom);
        state = state with
        {
            Drone = state.Drone with
            {
                PatrolRoute = [CoreScenario.SafeDroneRoom, CoreScenario.BetaRoom],
                PatrolIndex = 0,
                CurrentRoomId = CoreScenario.SafeDroneRoom,
            },
        };

        var result = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));
        var damage = result.Events
            .Where(item => item.Type == CanonicalEventType.AgentDamaged)
            .Select(item => Assert.IsType<AgentDamagedPayload>(item.Payload).AgentId)
            .ToArray();

        Assert.Equal([CoreScenario.KiteId, CoreScenario.WrenId], damage);
    }

    private static int EventIndex(TurnResult result, CanonicalEventType type) =>
        result.Events.IndexOf(result.Events.First(item => item.Type == type));
}
