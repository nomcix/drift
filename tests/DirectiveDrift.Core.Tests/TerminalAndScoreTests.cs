using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Scoring;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Core.Tests;

public sealed class TerminalAndScoreTests
{
    [Fact]
    public void DeadlineFailsAfterTurnEighteenButSuccessOnTurnEighteenWins()
    {
        var deadlineState = CoreScenario.Start() with { Turn = 17 };
        var failed = TurnResolver.ResolveTurn(
            deadlineState,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));

        Assert.Equal(RunStatus.Failed, failed.State.Status);
        Assert.Equal(MissionFailureReason.Deadline, failed.State.FailureReason);

        var successState = CoreScenario.WithArchiveOpen(CoreScenario.Start()) with { Turn = 17 };
        successState = CoreScenario.PlaceAgent(
            successState,
            CoreScenario.KiteId,
            CoreScenario.ExtractionRoom);
        successState = CoreScenario.PlaceAgent(
            successState,
            CoreScenario.WrenId,
            CoreScenario.ExtractionRoom);
        successState = CoreScenario.UpdateAgent(
            successState,
            CoreScenario.KiteId,
            agent => agent with { CarriedItemId = CoreScenario.RecorderId });
        successState = successState with
        {
            Recorder = successState.Recorder with
            {
                Condition = RecorderCondition.Carried,
                CarrierAgentId = CoreScenario.KiteId,
            },
        };

        var succeeded = TurnResolver.ResolveTurn(
            successState,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));
        Assert.Equal(RunStatus.Succeeded, succeeded.State.Status);
        Assert.Equal(18, succeeded.State.Turn);
    }

    [Fact]
    public void DisabledAgentCausesTerminalFailure()
    {
        var state = CoreScenario.Start(radiationOnStartLink: true);
        state = CoreScenario.UpdateAgent(
            state,
            CoreScenario.KiteId,
            agent => agent with { Health = 1 });

        var result = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("move:engineer-start"),
                CoreScenario.Decision("wait")));

        Assert.Equal(RunStatus.Failed, result.State.Status);
        Assert.Equal(MissionFailureReason.AgentDisabled, result.State.FailureReason);
        Assert.Equal(0, result.State.Agents.Single(agent => agent.AgentId == CoreScenario.KiteId).Health);
    }

    [Fact]
    public void RankedScoreUsesExactIntegerArithmetic()
    {
        var state = CoreScenario.Start(
            kiteModule: SupportModule.HazardShield,
            wrenModule: SupportModule.CargoClamp) with
        {
            Turn = 10,
            Status = RunStatus.Succeeded,
            Communication = CoreScenario.Start().Communication with { RemainingMessages = 3 },
        };

        var result = ScoreCalculator.Calculate(state);

        Assert.True(result.IsRanked);
        Assert.Equal(1740, result.Score);
    }

    [Fact]
    public void FailedAndAssistedRunsReceiveProgressRatherThanRankedScore()
    {
        var failed = ScoreCalculator.Calculate(CoreScenario.Start() with { Status = RunStatus.Failed });
        var assisted = ScoreCalculator.Calculate(
            CoreScenario.Start() with
            {
                Status = RunStatus.Succeeded,
                Score = CoreScenario.Start().Score with { Assisted = true },
            });

        Assert.False(failed.IsRanked);
        Assert.Null(failed.Score);
        Assert.False(assisted.IsRanked);
        Assert.Null(assisted.Score);
    }
}
