using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Tests;

public sealed class BuildAndDecisionReferenceTests
{
    [Theory]
    [InlineData("examples/designed-build.json")]
    [InlineData("examples/generic-optimal-build.json")]
    public void CanonicalBuildReferencesResolve(string buildPath)
    {
        var mission = LoadMission();
        var buildResult = ContractDocumentLoader.LoadBuild(
            RepositoryFiles.Read(buildPath),
            RepositoryFiles.Read("contracts/build.schema.json"));

        Assert.True(buildResult.IsValid);
        Assert.True(BuildReferenceValidator.Validate(buildResult.Document!, mission).IsValid);
    }

    [Fact]
    public void MissingBriefingCardReferenceIsRejected()
    {
        var build = RepositoryFiles.ReadObject("examples/designed-build.json");
        build["agents"]!["kite"]!["briefingCardIds"]!.AsArray()[0] = "missing-card";
        var buildResult = ContractDocumentLoader.LoadBuild(
            build.ToJsonString(),
            RepositoryFiles.Read("contracts/build.schema.json"));

        Assert.True(buildResult.IsValid);

        var report = BuildReferenceValidator.Validate(buildResult.Document!, LoadMission());

        Assert.Contains(
            report.Errors,
            error => error.Code == ValidationErrorCodes.ContentUnresolvedReference
                && error.Path == "/agents/kite/briefingCardIds/0");
    }

    [Fact]
    public void BuildAgentIdsMustExactlyMatchTheSelectedMissionRoster()
    {
        var build = RepositoryFiles.ReadObject("examples/designed-build.json");
        RenameBuildAgent(build, "kite", "rook");
        var buildResult = ContractDocumentLoader.LoadBuild(
            build.ToJsonString(),
            RepositoryFiles.Read("contracts/build.schema.json"));

        Assert.True(buildResult.IsValid, string.Join(Environment.NewLine, buildResult.Errors));

        var report = BuildReferenceValidator.Validate(buildResult.Document!, LoadMission());

        Assert.Contains(
            report.Errors,
            error => error.Code == ValidationErrorCodes.ContentUnresolvedReference
                && error.Path == "/agents/rook");
        Assert.Contains(
            report.Errors,
            error => error.Code == ValidationErrorCodes.ContentInvalidReference
                && error.Path == "/agents"
                && error.Message.Contains("kite", StringComparison.Ordinal));
    }

    [Fact]
    public void MatchingContentDefinedRosterAndBuildAreAccepted()
    {
        var mission = RepositoryFiles.ReadObject("content/missions/cold-start/mission.json");
        mission["agents"]!.AsArray()[0]!["agentId"] = "rook";
        mission["agents"]!.AsArray()[0]!["label"] = "Rook";
        mission["agents"]!.AsArray()[1]!["agentId"] = "lark";
        mission["agents"]!.AsArray()[1]!["label"] = "Lark";
        mission["connections"]!.AsArray()[11]!["allowedAgentIds"]!.AsArray()[0] = "rook";
        mission["devices"]!["generator"]!["repairAgentId"] = "lark";
        var missionResult = MissionLoader.Load(
            mission.ToJsonString(),
            RepositoryFiles.Read("contracts/mission.schema.json"));

        Assert.True(missionResult.IsValid, string.Join(Environment.NewLine, missionResult.Errors));

        var build = RepositoryFiles.ReadObject("examples/designed-build.json");
        RenameBuildAgent(build, "kite", "rook");
        RenameBuildAgent(build, "wren", "lark");
        var buildResult = ContractDocumentLoader.LoadBuild(
            build.ToJsonString(),
            RepositoryFiles.Read("contracts/build.schema.json"));

        Assert.True(buildResult.IsValid, string.Join(Environment.NewLine, buildResult.Errors));
        Assert.True(
            BuildReferenceValidator.Validate(buildResult.Document!, missionResult.Mission!).IsValid);
    }

    [Fact]
    public void LabelsAndCapabilitiesDoNotDefineBuildIdentity()
    {
        var mission = RepositoryFiles.ReadObject("content/missions/cold-start/mission.json");
        mission["agents"]!.AsArray()[0]!["label"] = "Survey Unit";
        mission["agents"]!.AsArray()[0]!["capabilities"] = new System.Text.Json.Nodes.JsonArray(
            "move",
            "carry-mission-item");
        var missionResult = MissionLoader.Load(
            mission.ToJsonString(),
            RepositoryFiles.Read("contracts/mission.schema.json"));
        var buildResult = ContractDocumentLoader.LoadBuild(
            RepositoryFiles.Read("examples/designed-build.json"),
            RepositoryFiles.Read("contracts/build.schema.json"));

        Assert.True(missionResult.IsValid, string.Join(Environment.NewLine, missionResult.Errors));
        Assert.True(buildResult.IsValid, string.Join(Environment.NewLine, buildResult.Errors));
        Assert.True(
            BuildReferenceValidator.Validate(buildResult.Document!, missionResult.Mission!).IsValid);
    }

    [Fact]
    public void CSharpBoundaryAlsoRejectsAOneAgentV1Build()
    {
        var buildResult = ContractDocumentLoader.LoadBuild(
            RepositoryFiles.Read("examples/designed-build.json"),
            RepositoryFiles.Read("contracts/build.schema.json"));
        var build = Assert.IsType<BuildDocument>(buildResult.Document);
        var oneAgentBuild = build with
        {
            Agents = build.Agents.Take(1).ToDictionary(),
        };

        var report = BuildReferenceValidator.Validate(oneAgentBuild, LoadMission());

        Assert.Contains(
            report.Errors,
            error => error.Code == ValidationErrorCodes.ContentInvariantFailed
                && error.Path == "/agents"
                && error.Message.Contains("exactly two", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownMessageRecipientIsRejected()
    {
        var decision = RepositoryFiles.ReadObject("examples/agent-decision.example.json");
        decision["message"]!["toAgentId"] = "rook";
        var decisionResult = ContractDocumentLoader.LoadAgentDecision(
            decision.ToJsonString(),
            RepositoryFiles.Read("contracts/agent-decision.schema.json"));

        Assert.True(decisionResult.IsValid);

        var report = DecisionReferenceValidator.Validate(
            decisionResult.Document!,
            LoadMission());

        var error = Assert.Single(report.Errors);
        Assert.Equal(ValidationErrorCodes.ContentUnresolvedReference, error.Code);
        Assert.Equal("/message/toAgentId", error.Path);
    }

    private static ValidatedMission LoadMission()
    {
        var result = MissionLoader.Load(
            RepositoryFiles.Read("content/missions/cold-start/mission.json"),
            RepositoryFiles.Read("contracts/mission.schema.json"));

        return Assert.IsType<ValidatedMission>(result.Mission);
    }

    private static void RenameBuildAgent(
        System.Text.Json.Nodes.JsonObject build,
        string existingAgentId,
        string replacementAgentId)
    {
        var agents = build["agents"]!.AsObject();
        var agentBuild = agents[existingAgentId]!.DeepClone();
        Assert.True(agents.Remove(existingAgentId));
        agents[replacementAgentId] = agentBuild;
    }
}
