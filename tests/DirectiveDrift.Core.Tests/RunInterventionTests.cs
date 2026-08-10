using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Core.Tests;

public sealed class RunInterventionTests
{
    [Fact]
    public void EmergencyBurstQueuesForBothAgentsConsumesBudgetAndMarksAssisted()
    {
        var state = CoreScenario.Start();

        var result = RunIntervention.ApplyEmergencyBurst(state, "Regroup at the safe route.");

        Assert.True(result.State.Score.Assisted);
        Assert.Equal(0, result.State.Communication.RemainingMessages);
        Assert.Equal(2, result.State.Communication.QueuedMessages.Length);
        Assert.All(result.State.Communication.QueuedMessages, value =>
        {
            Assert.Equal(1, value.DeliveryTurn);
            Assert.Equal("mission-control", value.SenderAgentId.Value);
        });
        Assert.All(result.Events, value => Assert.Equal(CanonicalEventType.MessageQueued, value.Type));
        Assert.Equal(result.State.NextEventSequence, state.NextEventSequence + 2);
    }

    [Fact]
    public void EmergencyBurstRejectsSecondUseAndInvalidText()
    {
        var first = RunIntervention.ApplyEmergencyBurst(CoreScenario.Start(), "Hold.");

        Assert.Throws<InvalidOperationException>(() =>
            RunIntervention.ApplyEmergencyBurst(first.State, "Again."));
        Assert.Throws<ArgumentException>(() =>
            RunIntervention.ApplyEmergencyBurst(CoreScenario.Start(), new string('x', 121)));
    }
}
