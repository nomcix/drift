using System.Collections.Immutable;
using System.Text.Json;
using DirectiveDrift.AI;
using DirectiveDrift.Application.Models;
using DirectiveDrift.EvaluationCli;

namespace DirectiveDrift.Evaluation;

public sealed class P10GateTests
{
    [Fact]
    public void CircuitBreakerRejectsBeforeDispatchAndSettlesBoundedUsage()
    {
        var profile = ProviderProfiles.OpenAi;
        var blocked = new EvaluationCircuitBreaker(3_639);

        Assert.Throws<EvaluationBudgetExceededException>(() => blocked.Reserve(profile, 2, 0));

        var allowed = new EvaluationCircuitBreaker(3_640);
        var reservation = allowed.Reserve(profile, 2, 0);
        reservation.Settle(1_000);
        Assert.Throws<InvalidOperationException>(() => reservation.Settle(1_000));
    }

    [Fact]
    public void GateEnforcesPinnedMatrixPerformanceGapAndInvalidRate()
    {
        var generic = Report("generic-optimal", successesPerVariant: 1, invalid: 0);
        var designed = Report("split-lantern", successesPerVariant: 4, invalid: 0);

        var result = P10GateEvaluator.Evaluate(generic, designed);

        Assert.True(result.Passed);
        Assert.Equal(8, result.GenericSuccesses);
        Assert.Equal(32, result.DesignedSuccesses);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void AggregateReportCannotSerializePrivateProviderMaterial()
    {
        var json = JsonSerializer.Serialize(Report("split-lantern", 4, 0));

        Assert.DoesNotContain("context", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("message", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rationale", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", json, StringComparison.OrdinalIgnoreCase);
    }

    private static LiveEvaluationReport Report(
        string buildId,
        int successesPerVariant,
        int invalid)
    {
        var runs = Enumerable.Range(1, 8)
            .SelectMany(variant => Enumerable.Range(1, 5).Select(repetition =>
                new LiveEvaluationRun(
                    buildId,
                    $"variant-{variant}",
                    repetition,
                    repetition <= successesPerVariant ? "succeeded" : "failed",
                    10,
                    0,
                    20,
                    variant == 1 && repetition == 1 ? invalid : 0,
                    0,
                    20,
                    1_000,
                    100,
                    500,
                    null,
                    [],
                    ImmutableDictionary<string, int>.Empty)))
            .ToImmutableArray();
        return new LiveEvaluationReport(
            "p10-live-evaluation-v1",
            buildId,
            ProviderProfiles.OpenAi.ProfileId,
            ProviderProfiles.OpenAi.Model,
            ProviderProfiles.OpenAi.PriceTableVersion,
            10_000_000,
            runs);
    }
}
