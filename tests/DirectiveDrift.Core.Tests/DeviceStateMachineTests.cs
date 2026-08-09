using System.Collections.Immutable;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Core.Tests;

public sealed class DeviceStateMachineTests
{
    [Fact]
    public void GeneratorBeginsContinuesAndCompletesAcrossTwoTurns()
    {
        var state = CoreScenario.PlaceAgent(
            CoreScenario.Start(),
            CoreScenario.WrenId,
            CoreScenario.GeneratorRoom);

        var started = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("wait"),
                CoreScenario.Decision("repair:generator")));

        Assert.Equal(GeneratorCondition.Repairing, started.State.Generator.Condition);
        Assert.Equal(CoreScenario.WrenId, started.State.Generator.RepairingAgentId);
        Assert.Contains(started.Events, item => item.Type == CanonicalEventType.RepairStarted);

        var completed = TurnResolver.ResolveTurn(
            started.State,
            CoreScenario.Decisions(
                CoreScenario.Decision("wait"),
                CoreScenario.Decision("continue-repair:generator")));

        Assert.Equal(GeneratorCondition.Online, completed.State.Generator.Condition);
        Assert.Contains(completed.Events, item => item.Type == CanonicalEventType.RepairContinued);
        Assert.Contains(completed.Events, item => item.Type == CanonicalEventType.PowerRestored);
    }

    [Theory]
    [InlineData("wait")]
    [InlineData("move:engineer-start")]
    public void AnotherPrimaryActionInterruptsGeneratorRepair(string secondAction)
    {
        var repairing = BeginGeneratorRepair(CoreScenario.Start());

        var result = TurnResolver.ResolveTurn(
            repairing,
            CoreScenario.Decisions(
                CoreScenario.Decision("wait"),
                CoreScenario.Decision(secondAction)));

        Assert.Equal(GeneratorCondition.Damaged, result.State.Generator.Condition);
        Assert.Equal(1, result.State.Score.InterruptedMajorRepairs);
        Assert.Contains(result.Events, item => item.Type == CanonicalEventType.RepairInterrupted);
    }

    [Fact]
    public void DamageInterruptsGeneratorContinuationBeforeObjectiveEvaluation()
    {
        var repairing = BeginGeneratorRepair(CoreScenario.Start());
        repairing = repairing with
        {
            Drone = repairing.Drone with
            {
                PatrolRoute = [CoreScenario.SafeDroneRoom, CoreScenario.GeneratorRoom],
                PatrolIndex = 0,
                CurrentRoomId = CoreScenario.SafeDroneRoom,
            },
        };

        var result = TurnResolver.ResolveTurn(
            repairing,
            CoreScenario.Decisions(
                CoreScenario.Decision("wait"),
                CoreScenario.Decision("continue-repair:generator")));

        Assert.Equal(GeneratorCondition.Damaged, result.State.Generator.Condition);
        Assert.Equal(1, result.State.Agents.Single(agent => agent.AgentId == CoreScenario.WrenId).Health);
        Assert.True(
            EventIndex(result, CanonicalEventType.AgentDamaged)
            < EventIndex(result, CanonicalEventType.RepairInterrupted));
    }

    [Fact]
    public void RapidRepairCompletesImmediatelyAndConsumesItsOnlyCharge()
    {
        var state = CoreScenario.PlaceAgent(
            CoreScenario.Start(wrenModule: SupportModule.RapidRepairKit),
            CoreScenario.WrenId,
            CoreScenario.GeneratorRoom);

        var result = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("wait"),
                CoreScenario.Decision("repair:generator")));

        var wren = result.State.Agents.Single(agent => agent.AgentId == CoreScenario.WrenId);
        Assert.Equal(GeneratorCondition.Online, result.State.Generator.Condition);
        Assert.Equal(0, wren.Module.ChargesRemaining);
        Assert.Single(result.Events.Where(item => item.Type == CanonicalEventType.ModuleConsumed));
    }

    [Fact]
    public void ConsoleRepairAndPowerOrderBothProduceAReadyConsole()
    {
        var repairBeforePower = CoreScenario.Start(betaCondition: ConsoleCondition.Damaged);
        repairBeforePower = CoreScenario.PlaceAgent(
            repairBeforePower,
            CoreScenario.WrenId,
            CoreScenario.BetaRoom);
        repairBeforePower = TurnResolver.ResolveTurn(
            repairBeforePower,
            CoreScenario.Decisions(
                CoreScenario.Decision("wait"),
                CoreScenario.Decision("repair:console-beta"))).State;
        repairBeforePower = CoreScenario.WithPowerOnline(repairBeforePower);

        var powerBeforeRepair = CoreScenario.WithPowerOnline(
            CoreScenario.Start(betaCondition: ConsoleCondition.Damaged));
        powerBeforeRepair = CoreScenario.PlaceAgent(
            powerBeforeRepair,
            CoreScenario.WrenId,
            CoreScenario.BetaRoom);
        powerBeforeRepair = TurnResolver.ResolveTurn(
            powerBeforeRepair,
            CoreScenario.Decisions(
                CoreScenario.Decision("wait"),
                CoreScenario.Decision("repair:console-beta"))).State;

        Assert.Equal(ConsoleCondition.Operational, repairBeforePower.ConsoleBeta.Condition);
        Assert.Equal(ConsoleCondition.Operational, powerBeforeRepair.ConsoleBeta.Condition);
        Assert.Contains(
            LegalActionGenerator.GetLegalActions(repairBeforePower, CoreScenario.WrenId).Actions,
            action => action.ActionId.Value == "activate:console-beta");
        Assert.Contains(
            LegalActionGenerator.GetLegalActions(powerBeforeRepair, CoreScenario.WrenId).Actions,
            action => action.ActionId.Value == "activate:console-beta");
    }

    [Fact]
    public void TwoDifferentActiveAgentsOnDifferentConsolesOpenGatePermanently()
    {
        var state = PlaceAgentsAtPoweredConsoles(CoreScenario.Start());

        var result = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("activate:console-alpha"),
                CoreScenario.Decision("activate:console-beta")));

        Assert.True(result.State.ArchiveGateOpen);
        Assert.Equal(RecorderCondition.Available, result.State.Recorder.Condition);
        Assert.Contains(result.Events, item => item.Type == CanonicalEventType.ArchiveOpened);

        var later = TurnResolver.ResolveTurn(
            result.State,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));
        Assert.True(later.State.ArchiveGateOpen);
    }

    [Fact]
    public void UnmatchedActivationFailsAndLeavesBothConsolesReadyToRetry()
    {
        var state = PlaceAgentsAtPoweredConsoles(CoreScenario.Start());

        var failed = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("activate:console-alpha"),
                CoreScenario.Decision("wait")));

        Assert.False(failed.State.ArchiveGateOpen);
        Assert.Equal(1, failed.State.Score.FailedConsoleActivations);
        Assert.Contains(failed.Events, item => item.Type == CanonicalEventType.ConsoleSyncFailed);
        Assert.Contains(
            LegalActionGenerator.GetLegalActions(failed.State, CoreScenario.KiteId).Actions,
            action => action.ActionId.Value == "activate:console-alpha");
        Assert.Contains(
            LegalActionGenerator.GetLegalActions(failed.State, CoreScenario.WrenId).Actions,
            action => action.ActionId.Value == "activate:console-beta");
    }

    [Fact]
    public void ActivatingTheSameConsoleWithTwoAgentsDoesNotSatisfySync()
    {
        var state = CoreScenario.WithPowerOnline(CoreScenario.Start());
        state = CoreScenario.PlaceAgent(state, CoreScenario.KiteId, CoreScenario.AlphaRoom);
        state = CoreScenario.PlaceAgent(state, CoreScenario.WrenId, CoreScenario.AlphaRoom);

        var result = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("activate:console-alpha"),
                CoreScenario.Decision("activate:console-alpha")));

        Assert.False(result.State.ArchiveGateOpen);
        Assert.Equal(1, result.State.Score.FailedConsoleActivations);
    }

    [Fact]
    public void AgentDisabledDuringThreatCannotCompleteConsoleSync()
    {
        var state = PlaceAgentsAtPoweredConsoles(CoreScenario.Start());
        state = CoreScenario.UpdateAgent(
            state,
            CoreScenario.WrenId,
            agent => agent with { Health = 1 });
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
            CoreScenario.Decisions(
                CoreScenario.Decision("activate:console-alpha"),
                CoreScenario.Decision("activate:console-beta")));

        Assert.False(result.State.ArchiveGateOpen);
        Assert.Contains(result.Events, item => item.Type == CanonicalEventType.ConsoleSyncFailed);
        Assert.Equal(RunStatus.Failed, result.State.Status);
    }

    private static RunState BeginGeneratorRepair(RunState state)
    {
        state = CoreScenario.PlaceAgent(state, CoreScenario.WrenId, CoreScenario.GeneratorRoom);
        return TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("wait"),
                CoreScenario.Decision("repair:generator"))).State;
    }

    private static RunState PlaceAgentsAtPoweredConsoles(RunState state)
    {
        state = CoreScenario.WithPowerOnline(state);
        state = CoreScenario.PlaceAgent(state, CoreScenario.KiteId, CoreScenario.AlphaRoom);
        return CoreScenario.PlaceAgent(state, CoreScenario.WrenId, CoreScenario.BetaRoom);
    }

    private static int EventIndex(TurnResult result, CanonicalEventType type) =>
        result.Events.IndexOf(result.Events.Single(item => item.Type == type));
}
