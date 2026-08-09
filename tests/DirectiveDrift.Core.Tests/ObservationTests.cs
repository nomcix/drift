using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Observations;

namespace DirectiveDrift.Core.Tests;

public sealed class ObservationTests
{
    [Fact]
    public void ReconSensesAdjacentRadiationButEngineerDoesNot()
    {
        var state = CoreScenario.Start(radiationOnStartLink: true);
        state = CoreScenario.PlaceAgent(state, CoreScenario.WrenId, CoreScenario.ExtractionRoom);

        var kite = PrivateObservationBuilder.Build(state, CoreScenario.KiteId);
        var wren = PrivateObservationBuilder.Build(state, CoreScenario.WrenId);

        Assert.Equal(HazardObservation.Radiation, Assert.Single(kite.Exits).Hazard);
        Assert.Equal(HazardObservation.Unknown, Assert.Single(wren.Exits).Hazard);
    }

    [Fact]
    public void ObservationDoesNotExposeRemotePartnerOrUndeliveredMessage()
    {
        var state = CoreScenario.Start();
        var queued = new AgentMessage(
            new MessageId("queued"),
            CoreScenario.WrenId,
            CoreScenario.KiteId,
            0,
            1,
            "private");
        state = state with
        {
            Communication = state.Communication with { QueuedMessages = [queued] },
        };

        var observation = PrivateObservationBuilder.Build(state, CoreScenario.KiteId);

        Assert.Empty(observation.DeliveredMessages);
        Assert.DoesNotContain(
            observation.LocalEntities,
            entity => entity.EntityId == CoreScenario.WrenId.Value);
    }

    [Fact]
    public void PartnerIsVisibleOnlyWhenLocalAndMachineryDiagnosisIsPrivate()
    {
        var state = CoreScenario.Start();
        state = CoreScenario.PlaceAgent(state, CoreScenario.KiteId, CoreScenario.GeneratorRoom);
        state = CoreScenario.PlaceAgent(state, CoreScenario.WrenId, CoreScenario.GeneratorRoom);

        var kite = PrivateObservationBuilder.Build(state, CoreScenario.KiteId);
        var wren = PrivateObservationBuilder.Build(state, CoreScenario.WrenId);

        Assert.Contains(kite.LocalEntities, entity => entity.EntityId == CoreScenario.WrenId.Value);
        Assert.Null(kite.LocalEntities.Single(entity => entity.Kind == ObservedEntityKind.Generator)
            .DiagnosedState);
        Assert.Equal(
            GeneratorCondition.Damaged.ToString(),
            wren.LocalEntities.Single(entity => entity.Kind == ObservedEntityKind.Generator)
                .DiagnosedState);
    }
}
