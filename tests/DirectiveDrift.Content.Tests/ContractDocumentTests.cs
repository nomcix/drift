using System.Text.Json.Nodes;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Contracts;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Validation;

namespace DirectiveDrift.Content.Tests;

public sealed class ContractDocumentTests
{
    [Fact]
    public void CheckedInContractsAndExamplesMatchTheWorkpack()
    {
        var relativePaths = new[]
        {
            "contracts/agent-decision.schema.json",
            "contracts/build.schema.json",
            "contracts/mission.schema.json",
            "examples/agent-decision.example.json",
            "examples/cold-start.mission.json",
            "examples/designed-build.json",
            "examples/generic-optimal-build.json",
        };

        foreach (var path in relativePaths)
        {
            Assert.Equal(
                RepositoryFiles.Read($"docs/workpack/{path}"),
                RepositoryFiles.Read(path));
        }

        Assert.Equal(
            RepositoryFiles.Read("examples/cold-start.mission.json"),
            RepositoryFiles.Read("content/missions/cold-start/mission.json"));
    }

    [Fact]
    public void ContractVersionConstantsMatchCanonicalDocuments()
    {
        var mission = RepositoryFiles.ReadObject("examples/cold-start.mission.json");
        var build = RepositoryFiles.ReadObject("examples/designed-build.json");
        var decision = RepositoryFiles.ReadObject("examples/agent-decision.example.json");

        Assert.Equal(ContractVersions.Mission, mission["schemaVersion"]?.GetValue<string>());
        Assert.Equal(ContractVersions.Build, build["schemaVersion"]?.GetValue<string>());
        Assert.Equal(
            ContractVersions.AgentDecision,
            decision["schemaVersion"]?.GetValue<string>());
    }

    [Fact]
    public void MissionRoundTripsWithoutSemanticLoss()
    {
        AssertSemanticRoundTrip(
            "examples/cold-start.mission.json",
            "contracts/mission.schema.json",
            ContractDocumentLoader.LoadMission);
    }

    [Theory]
    [InlineData("examples/designed-build.json")]
    [InlineData("examples/generic-optimal-build.json")]
    public void BuildRoundTripsWithoutSemanticLoss(string examplePath)
    {
        AssertSemanticRoundTrip(
            examplePath,
            "contracts/build.schema.json",
            ContractDocumentLoader.LoadBuild);
    }

    [Fact]
    public void BuildAgentMapAcceptsOpaqueContentDefinedIds()
    {
        var build = RepositoryFiles.ReadObject("examples/designed-build.json");
        var agents = build["agents"]!.AsObject();
        var reassignedBuild = agents["kite"]!.DeepClone();
        Assert.True(agents.Remove("kite"));
        agents["rook"] = reassignedBuild;

        var result = ContractDocumentLoader.LoadBuild(
            build.ToJsonString(),
            RepositoryFiles.Read("contracts/build.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Contains("rook", result.Document!.Agents.Keys);
        Assert.Equal(2, result.Document.Agents.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void BuildAgentMapRequiresExactlyTwoEntries(int agentCount)
    {
        var build = RepositoryFiles.ReadObject("examples/designed-build.json");
        var agents = build["agents"]!.AsObject();

        if (agentCount == 1)
        {
            Assert.True(agents.Remove("wren"));
        }
        else
        {
            agents["rook"] = agents["kite"]!.DeepClone();
        }

        var result = ContractDocumentLoader.LoadBuild(
            build.ToJsonString(),
            RepositoryFiles.Read("contracts/build.schema.json"));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.SchemaViolation
                && error.Path == "/agents");
    }

    [Fact]
    public void AgentDecisionRoundTripsWithoutSemanticLoss()
    {
        AssertSemanticRoundTrip(
            "examples/agent-decision.example.json",
            "contracts/agent-decision.schema.json",
            ContractDocumentLoader.LoadAgentDecision);
    }

    [Fact]
    public void UnknownPropertiesAreRejected()
    {
        var build = RepositoryFiles.ReadObject("examples/designed-build.json");
        build["unexpected"] = true;

        var result = ContractDocumentLoader.LoadBuild(
            build.ToJsonString(),
            RepositoryFiles.Read("contracts/build.schema.json"));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.SchemaAdditionalProperties);
    }

    [Fact]
    public void StrictDtoDeserializationAlsoRejectsUnknownProperties()
    {
        var build = RepositoryFiles.ReadObject("examples/designed-build.json");
        build["unexpected"] = true;

        Assert.ThrowsAny<System.Text.Json.JsonException>(
            () => ContractJson.Deserialize<BuildDocument>(build.ToJsonString()));
    }

    [Fact]
    public void MalformedJsonHasAStableErrorCode()
    {
        var result = ContractDocumentLoader.LoadMission(
            RepositoryFiles.Read(
                "tests/DirectiveDrift.Content.Tests/Fixtures/Invalid/malformed-mission.json"),
            RepositoryFiles.Read("contracts/mission.schema.json"));

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.JsonMalformed, error.Code);
    }

    private static void AssertSemanticRoundTrip<T>(
        string examplePath,
        string schemaPath,
        Func<string, string, DocumentLoadResult<T>> load)
        where T : class
    {
        var originalJson = RepositoryFiles.Read(examplePath);
        var result = load(originalJson, RepositoryFiles.Read(schemaPath));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.NotNull(result.Document);

        var original = JsonNode.Parse(originalJson);
        var roundTripped = JsonNode.Parse(ContractJson.Serialize(result.Document));

        Assert.True(JsonNode.DeepEquals(original, roundTripped));
    }
}
