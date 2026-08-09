using DirectiveDrift.Content.Evaluation;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Evaluation;

public sealed class ScriptedEvaluationTests
{
    [Fact]
    public void GenericTutorialFailsForTheMissingSyncContract()
    {
        var mission = RepositoryContent.LoadMission();
        var catalog = RepositoryContent.LoadCatalog(mission);
        var build = RepositoryContent.LoadBuild("examples/generic-optimal-build.json");

        var report = ScriptedEvaluationRunner.Run(
            mission,
            catalog,
            build,
            new EvaluationRequest(
                EvaluationProviderMode.Scripted,
                EvaluationMatrixSelector.Select(catalog, "tutorial"),
                1));

        var run = Assert.Single(report.Runs);
        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("unknown-required-contract", run.FailureSignature);
        Assert.InRange(run.Turns, 1, 18);
        Assert.Equal(0, run.FallbackDecisions);
    }

    [Fact]
    public void CorrectedTutorialSucceedsWithoutFallbacks()
    {
        var mission = RepositoryContent.LoadMission();
        var catalog = RepositoryContent.LoadCatalog(mission);
        var build = RepositoryContent.LoadBuild("examples/designed-build.json");

        var report = ScriptedEvaluationRunner.Run(
            mission,
            catalog,
            build,
            new EvaluationRequest(
                EvaluationProviderMode.Scripted,
                EvaluationMatrixSelector.Select(catalog, "tutorial"),
                2));

        Assert.Equal(2, report.Successes);
        Assert.All(report.Runs, run => Assert.Equal(RunStatus.Succeeded, run.Status));
        Assert.Equal(0, report.FallbackDecisions);
        Assert.Equal(
            report.Runs[0] with { Repetition = 2 },
            report.Runs[1]);
    }

    [Fact]
    public void PinnedMatrixContainsFivePracticeAndThreeHeldOutVariants()
    {
        var mission = RepositoryContent.LoadMission();
        var catalog = RepositoryContent.LoadCatalog(mission);

        var matrix = EvaluationMatrixSelector.Select(catalog, "pinned");

        Assert.Equal(8, matrix.Length);
        Assert.Equal(5, matrix.Count(variantId => variantId.Value.StartsWith(
            "cs-practice-",
            StringComparison.Ordinal)));
        Assert.Equal(3, matrix.Count(variantId => variantId.Value.StartsWith(
            "cs-cert-",
            StringComparison.Ordinal)));
    }
}
