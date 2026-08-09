using System.Collections.Immutable;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Serialization;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Core.Tests;

public sealed class DeterminismAndPropertyTests
{
    [Fact]
    public void SameStateAndDecisionsProduceByteEquivalentEventsAndHashes()
    {
        var state = CoreScenario.Start();
        var decisions = CoreScenario.Decisions(
            CoreScenario.Decision("move:engineer-start", "crossing"),
            CoreScenario.Decision("move:generator-room", memory: "repair next"));

        var first = TurnResolver.ResolveTurn(state, decisions);
        var second = TurnResolver.ResolveTurn(state, decisions);

        Assert.Equal(first.StateHash, second.StateHash);
        Assert.Equal(
            CanonicalEventSerializer.Serialize(first.Events),
            CanonicalEventSerializer.Serialize(second.Events));
    }

    [Fact]
    public void DecisionAndStateInsertionOrderDoNotAffectResolution()
    {
        var state = CoreScenario.Start();
        var reordered = state with
        {
            Agents = state.Agents.Reverse().ToImmutableArray(),
            Connections = state.Connections.Reverse().ToImmutableArray(),
        };
        var normal = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("move:engineer-start"),
                CoreScenario.Decision("move:extraction")));
        var reversed = TurnResolver.ResolveTurn(
            reordered,
            CoreScenario.Decisions(
                CoreScenario.Decision("move:engineer-start"),
                CoreScenario.Decision("move:extraction"),
                reverseInsertion: true));

        Assert.Equal(normal.StateHash, reversed.StateHash);
        Assert.Equal(
            CanonicalEventSerializer.Serialize(normal.Events),
            CanonicalEventSerializer.Serialize(reversed.Events));
    }

    [Fact]
    public void CanonicalStateRoundTripPreservesTheNextTurnResult()
    {
        var state = TurnResolver.ResolveTurn(
            CoreScenario.Start(),
            CoreScenario.Decisions(
                CoreScenario.Decision("wait", "hello", "memory"),
                CoreScenario.Decision("move:generator-room"))).State;
        var roundTripped = CanonicalStateSerializer.Deserialize(
            CanonicalStateSerializer.Serialize(state));
        var decisions = CoreScenario.Decisions(
            CoreScenario.Decision("wait"),
            CoreScenario.Decision("repair:generator"));

        var expected = TurnResolver.ResolveTurn(state, decisions);
        var actual = TurnResolver.ResolveTurn(roundTripped, decisions);

        Assert.Equal(expected.StateHash, actual.StateHash);
        Assert.Equal(
            CanonicalEventSerializer.Serialize(expected.Events),
            CanonicalEventSerializer.Serialize(actual.Events));
    }

    [Fact]
    public void CanonicalEventsRoundTripWithoutSemanticLoss()
    {
        var result = TurnResolver.ResolveTurn(
            CoreScenario.Start(),
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));

        var roundTripped = CanonicalEventSerializer.Deserialize(
            CanonicalEventSerializer.Serialize(result.Events));

        Assert.True(result.Events.SequenceEqual(roundTripped));
    }

    [Fact]
    public void ResolvedTurnsPreserveCoreInvariantsAcrossAStateWalk()
    {
        var state = CoreScenario.Start();
        var previousMessageBudget = state.Communication.RemainingMessages;

        for (var index = 0; index < 8; index++)
        {
            var kite = state.Agents.Single(agent => agent.AgentId == CoreScenario.KiteId);
            var wren = state.Agents.Single(agent => agent.AgentId == CoreScenario.WrenId);
            var kiteAction = kite.RoomId == CoreScenario.ExtractionRoom
                ? "move:engineer-start"
                : "move:extraction";
            var wrenAction = wren.RoomId == CoreScenario.EngineerStartRoom
                ? "move:generator-room"
                : "move:engineer-start";
            var beforeTurn = state.Turn;
            var result = TurnResolver.ResolveTurn(
                state,
                CoreScenario.Decisions(
                    CoreScenario.Decision(kiteAction, index == 0 ? "hello" : null),
                    CoreScenario.Decision(wrenAction)));

            Assert.Equal(beforeTurn + 1, result.State.Turn);
            Assert.Equal(
                Enumerable.Range(0, result.Events.Length)
                    .Select(offset => state.NextEventSequence + offset),
                result.Events.Select(item => item.Sequence));
            Assert.All(
                result.State.Agents,
                agent => Assert.InRange(agent.Health, 0, agent.MaxHealth));
            Assert.All(
                result.State.Agents,
                agent => Assert.Contains(agent.RoomId, result.State.Rooms));
            Assert.True(result.State.Communication.RemainingMessages <= previousMessageBudget);
            AssertAcceptedActionsWereLegal(result);
            AssertRecorderCustodyIsSingular(result.State);

            previousMessageBudget = result.State.Communication.RemainingMessages;
            state = result.State;
        }
    }

    [Fact]
    public void TurnEndedCarriesTheExactCanonicalPostStateHash()
    {
        var result = TurnResolver.ResolveTurn(
            CoreScenario.Start(),
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));
        var ended = result.Events[^1];

        Assert.Equal(CanonicalEventType.TurnEnded, ended.Type);
        Assert.Equal(result.StateHash, ended.PostStateHash);
        Assert.Equal(result.StateHash, Assert.IsType<TurnEndedPayload>(ended.Payload).StateHash);
        Assert.Equal(result.StateHash, CanonicalStateSerializer.Hash(result.State));
    }

    private static void AssertAcceptedActionsWereLegal(TurnResult result)
    {
        foreach (var decision in result.Decisions.Where(decision => !decision.UsedFallback))
        {
            var observation = result.Observations.Single(item => item.AgentId == decision.AgentId);
            Assert.Contains(
                observation.LegalActions.Actions,
                action => action.ActionId == decision.Action.ActionId);
        }
    }

    private static void AssertRecorderCustodyIsSingular(RunState state)
    {
        var carriers = state.Agents.Count(agent => agent.CarriedItemId == state.Recorder.ItemId);
        switch (state.Recorder.Condition)
        {
            case RecorderCondition.Carried:
                Assert.Equal(1, carriers);
                Assert.NotNull(state.Recorder.CarrierAgentId);
                Assert.Null(state.Recorder.DroppedRoomId);
                break;
            case RecorderCondition.Dropped:
                Assert.Equal(0, carriers);
                Assert.Null(state.Recorder.CarrierAgentId);
                Assert.NotNull(state.Recorder.DroppedRoomId);
                break;
            default:
                Assert.Equal(0, carriers);
                Assert.Null(state.Recorder.CarrierAgentId);
                Assert.Null(state.Recorder.DroppedRoomId);
                break;
        }
    }
}
