using DirectiveDrift.Core.Events;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Simulation;

namespace DirectiveDrift.Core.Tests;

public sealed class RecorderAndModuleTests
{
    [Fact]
    public void RecorderCanBePickedUpDroppedRepickedAndExtracted()
    {
        var state = CoreScenario.WithArchiveOpen(CoreScenario.Start());
        state = CoreScenario.PlaceAgent(state, CoreScenario.KiteId, CoreScenario.ArchiveRoom);

        var pickedUp = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("pickup:flight-recorder"),
                CoreScenario.Decision("wait"))).State;
        Assert.Equal(RecorderCondition.Carried, pickedUp.Recorder.Condition);

        pickedUp = pickedUp with
        {
            Drone = pickedUp.Drone with
            {
                PatrolRoute = [CoreScenario.SafeDroneRoom, CoreScenario.ArchiveRoom],
                PatrolIndex = 0,
                CurrentRoomId = CoreScenario.SafeDroneRoom,
            },
        };
        var dropped = TurnResolver.ResolveTurn(
            pickedUp,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")))
            .State;
        Assert.Equal(RecorderCondition.Dropped, dropped.Recorder.Condition);
        Assert.Equal(CoreScenario.ArchiveRoom, dropped.Recorder.DroppedRoomId);

        dropped = dropped with
        {
            Drone = dropped.Drone with
            {
                PatrolRoute = [CoreScenario.SafeDroneRoom],
                PatrolIndex = 0,
                CurrentRoomId = CoreScenario.SafeDroneRoom,
            },
        };
        var repicked = TurnResolver.ResolveTurn(
            dropped,
            CoreScenario.Decisions(
                CoreScenario.Decision("pickup:flight-recorder"),
                CoreScenario.Decision("wait"))).State;
        Assert.Equal(CoreScenario.KiteId, repicked.Recorder.CarrierAgentId);

        repicked = CoreScenario.PlaceAgent(repicked, CoreScenario.KiteId, CoreScenario.ExtractionRoom);
        repicked = CoreScenario.PlaceAgent(repicked, CoreScenario.WrenId, CoreScenario.ExtractionRoom);
        var extracted = TurnResolver.ResolveTurn(
            repicked,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));

        Assert.Equal(RecorderCondition.Extracted, extracted.State.Recorder.Condition);
        Assert.Equal(RunStatus.Succeeded, extracted.State.Status);
    }

    [Fact]
    public void HazardShieldPreventsExactlyOneRadiationDamage()
    {
        var state = CoreScenario.Start(
            kiteModule: SupportModule.HazardShield,
            radiationOnStartLink: true);

        var shielded = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("move:engineer-start"),
                CoreScenario.Decision("wait"))).State;
        var kite = shielded.Agents.Single(agent => agent.AgentId == CoreScenario.KiteId);
        Assert.Equal(2, kite.Health);
        Assert.Equal(0, kite.Module.ChargesRemaining);

        var unshielded = TurnResolver.ResolveTurn(
            shielded,
            CoreScenario.Decisions(
                CoreScenario.Decision("move:extraction"),
                CoreScenario.Decision("wait"))).State;
        Assert.Equal(
            1,
            unshielded.Agents.Single(agent => agent.AgentId == CoreScenario.KiteId).Health);
    }

    [Fact]
    public void CargoClampPreventsExactlyOneForcedRecorderDrop()
    {
        var state = CoreScenario.WithArchiveOpen(
            CoreScenario.Start(kiteModule: SupportModule.CargoClamp));
        state = CoreScenario.PlaceAgent(state, CoreScenario.KiteId, CoreScenario.ArchiveRoom);
        state = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("pickup:flight-recorder"),
                CoreScenario.Decision("wait"))).State;
        state = state with
        {
            Drone = state.Drone with
            {
                PatrolRoute = [CoreScenario.SafeDroneRoom, CoreScenario.ArchiveRoom],
                PatrolIndex = 0,
                CurrentRoomId = CoreScenario.SafeDroneRoom,
            },
        };

        var clamped = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));
        Assert.Equal(RecorderCondition.Carried, clamped.State.Recorder.Condition);
        Assert.Contains(clamped.Events, item => item.Type == CanonicalEventType.ModuleConsumed);

        var hitAgain = clamped.State with
        {
            Drone = clamped.State.Drone with
            {
                PatrolRoute = [CoreScenario.SafeDroneRoom, CoreScenario.ArchiveRoom],
                PatrolIndex = 0,
                CurrentRoomId = CoreScenario.SafeDroneRoom,
            },
        };
        var dropped = TurnResolver.ResolveTurn(
            hitAgain,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));
        Assert.Equal(RecorderCondition.Dropped, dropped.State.Recorder.Condition);
    }

    [Fact]
    public void DecoyOverridesExactlyTwoDroneSteps()
    {
        var state = CoreScenario.Start(kiteModule: SupportModule.DecoyBeacon);
        state = state with
        {
            Drone = state.Drone with
            {
                PatrolRoute = [CoreScenario.SafeDroneRoom, CoreScenario.GeneratorRoom],
                PatrolIndex = 0,
            },
        };

        var first = TurnResolver.ResolveTurn(
            state,
            CoreScenario.Decisions(
                CoreScenario.Decision("deploy:decoy-beacon"),
                CoreScenario.Decision("wait")));
        var second = TurnResolver.ResolveTurn(
            first.State,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));
        var third = TurnResolver.ResolveTurn(
            second.State,
            CoreScenario.Decisions(CoreScenario.Decision("wait"), CoreScenario.Decision("wait")));

        Assert.Equal(CoreScenario.ExtractionRoom, first.State.Drone.CurrentRoomId);
        Assert.Equal(CoreScenario.ExtractionRoom, second.State.Drone.CurrentRoomId);
        Assert.Equal(CoreScenario.GeneratorRoom, third.State.Drone.CurrentRoomId);
    }
}
