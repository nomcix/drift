using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;
using System.Collections.Immutable;

namespace DirectiveDrift.Core.Tests;

public sealed class RunStartFactoryTests
{
    [Fact]
    public void StartCreatesTheTurnZeroSnapshotAndCanonicalEvent()
    {
        var result = RunStartFactory.Create(
            new RunId("start-test"),
            CoreScenario.CreateDefinition(),
            42,
            54);

        Assert.Equal(0, result.State.Turn);
        Assert.Equal(RunStatus.Active, result.State.Status);
        Assert.Equal(2, result.State.Agents.Length);
        Assert.All(result.State.Agents, agent => Assert.Equal(AgentStatus.Active, agent.Status));
        Assert.All(result.State.Agents, agent => Assert.Equal(agent.MaxHealth, agent.Health));
        Assert.Equal(GeneratorCondition.Damaged, result.State.Generator.Condition);
        Assert.Equal(RecorderCondition.Secured, result.State.Recorder.Condition);
        Assert.Equal(6, result.State.Communication.RemainingMessages);
        Assert.Equal(CanonicalEventType.RunStarted, result.Event.Type);
        Assert.NotNull(result.Event.PostStateHash);
    }

    [Fact]
    public void SignalRepeaterAddsExactlyTwoMessagesAtStart()
    {
        var state = CoreScenario.Start(wrenModule: SupportModule.SignalRepeater);

        Assert.Equal(8, state.Communication.RemainingMessages);
        Assert.Equal(0, state.Agents.Single(agent => agent.AgentId == CoreScenario.WrenId)
            .Module.ChargesRemaining);
    }

    [Fact]
    public void InvalidDefinitionIsRejectedBeforeAStateExists()
    {
        var definition = CoreScenario.CreateDefinition() with { Rooms = [] };

        var exception = Assert.Throws<ArgumentException>(() =>
            RunStartFactory.Create(new RunId("invalid"), definition, 1, 1));

        Assert.Contains("room", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void StartRejectsAnyRosterThatDoesNotContainExactlyTwoDistinctAgents(int agentCount)
    {
        var definition = CoreScenario.CreateDefinition();
        var agents = agentCount == 1
            ? definition.Agents.Take(1).ToImmutableArray()
            : definition.Agents.Add(
                definition.Agents[0] with { AgentId = new AgentId("rook") });

        var exception = Assert.Throws<ArgumentException>(() =>
            RunStartFactory.Create(
                new RunId("invalid-roster"),
                definition with { Agents = agents },
                1,
                1));

        Assert.Contains("exactly two", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
