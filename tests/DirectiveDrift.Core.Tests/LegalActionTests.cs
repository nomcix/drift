using System.Collections.Immutable;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Core.Tests;

public sealed class LegalActionTests
{
    [Fact]
    public void WaitAlwaysExistsForActiveAgentsAndTerminalRunsHaveNoActions()
    {
        var state = CoreScenario.Start();

        Assert.Contains(
            LegalActionGenerator.GetLegalActions(state, CoreScenario.KiteId).Actions,
            action => action.ActionId == new ActionId("wait"));

        var terminal = state with { Status = RunStatus.Failed };
        Assert.Empty(LegalActionGenerator.GetLegalActions(terminal, CoreScenario.KiteId).Actions);
    }

    [Fact]
    public void CapabilityDifferencesControlScanAndRepairActions()
    {
        var state = CoreScenario.Start();
        state = CoreScenario.PlaceAgent(state, CoreScenario.KiteId, CoreScenario.GeneratorRoom);
        state = CoreScenario.PlaceAgent(state, CoreScenario.WrenId, CoreScenario.GeneratorRoom);

        var kite = LegalActionGenerator.GetLegalActions(state, CoreScenario.KiteId).Actions;
        var wren = LegalActionGenerator.GetLegalActions(state, CoreScenario.WrenId).Actions;

        Assert.Contains(kite, action => action.Kind == LegalActionKind.Scan);
        Assert.DoesNotContain(kite, action => action.Kind == LegalActionKind.RepairGenerator);
        Assert.DoesNotContain(wren, action => action.Kind == LegalActionKind.Scan);
        Assert.Contains(wren, action => action.Kind == LegalActionKind.RepairGenerator);
    }

    [Fact]
    public void CrawlspaceAndServiceLockUseCapabilityAndPowerState()
    {
        var state = DirectiveDrift.Core.Simulation.TurnResolver.ResolveTurn(
            CoreScenario.Start(),
            CoreScenario.Decisions(
                CoreScenario.Decision("move:engineer-start"),
                CoreScenario.Decision("wait"))).State;

        var kiteBeforePower = LegalActionGenerator.GetLegalActions(state, CoreScenario.KiteId).Actions;
        var wrenBeforePower = LegalActionGenerator.GetLegalActions(state, CoreScenario.WrenId).Actions;

        Assert.Contains(kiteBeforePower, action => action.ActionId.Value == "move:crawl-room");
        Assert.DoesNotContain(wrenBeforePower, action => action.ActionId.Value == "move:crawl-room");
        Assert.DoesNotContain(kiteBeforePower, action => action.ActionId.Value == "move:service-room");

        var powered = CoreScenario.WithPowerOnline(state);
        Assert.Contains(
            LegalActionGenerator.GetLegalActions(powered, CoreScenario.WrenId).Actions,
            action => action.ActionId.Value == "move:service-room");
    }

    [Fact]
    public void UndiscoveredConnectionCannotBeTargeted()
    {
        var state = CoreScenario.Start();
        state = CoreScenario.UpdateAgent(
            state,
            CoreScenario.KiteId,
            agent => agent with { DiscoveredConnections = [] });

        var actions = LegalActionGenerator.GetLegalActions(state, CoreScenario.KiteId).Actions;
        var wrenActions = LegalActionGenerator.GetLegalActions(state, CoreScenario.WrenId).Actions;

        Assert.DoesNotContain(actions, action => action.Kind == LegalActionKind.Move);
        Assert.DoesNotContain(wrenActions, action => action.ActionId.Value == "move:archive-room");
    }

    [Fact]
    public void ContinuationExistsOnlyForTheCommittedAgentInTheValidState()
    {
        var state = CoreScenario.Start();
        state = CoreScenario.PlaceAgent(state, CoreScenario.WrenId, CoreScenario.GeneratorRoom);
        state = state with
        {
            Generator = state.Generator with
            {
                Condition = GeneratorCondition.Repairing,
                RepairingAgentId = CoreScenario.WrenId,
            },
        };

        Assert.Contains(
            LegalActionGenerator.GetLegalActions(state, CoreScenario.WrenId).Actions,
            action => action.ActionId.Value == "continue-repair:generator");

        var damaged = state with
        {
            Generator = state.Generator with
            {
                Condition = GeneratorCondition.Damaged,
                RepairingAgentId = null,
            },
        };
        Assert.DoesNotContain(
            LegalActionGenerator.GetLegalActions(damaged, CoreScenario.WrenId).Actions,
            action => action.Kind == LegalActionKind.ContinueGeneratorRepair);
    }

    [Fact]
    public void DecoyActionRequiresAnUnusedEquippedModule()
    {
        var state = CoreScenario.Start(kiteModule: SupportModule.DecoyBeacon);

        Assert.Contains(
            LegalActionGenerator.GetLegalActions(state, CoreScenario.KiteId).Actions,
            action => action.ActionId.Value == "deploy:decoy-beacon");

        state = CoreScenario.UpdateAgent(
            state,
            CoreScenario.KiteId,
            agent => agent with { Module = agent.Module with { ChargesRemaining = 0 } });
        Assert.DoesNotContain(
            LegalActionGenerator.GetLegalActions(state, CoreScenario.KiteId).Actions,
            action => action.Kind == LegalActionKind.DeployDecoyBeacon);
    }

    [Fact]
    public void MovementTargetsAreGeneratedInOrdinalOrderRegardlessOfConnectionOrder()
    {
        var state = CoreScenario.Start();
        var reversed = state with { Connections = state.Connections.Reverse().ToImmutableArray() };

        var expected = LegalActionGenerator.GetLegalActions(state, CoreScenario.WrenId).Actions;
        var actual = LegalActionGenerator.GetLegalActions(reversed, CoreScenario.WrenId).Actions;

        Assert.True(expected.SequenceEqual(actual));
    }
}
