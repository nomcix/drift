using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Validation;

namespace DirectiveDrift.Content.Tests;

public sealed class InvalidFixtureTests
{
    public static TheoryData<string, string, string, string> Cases =>
        new()
        {
            {
                "tests/DirectiveDrift.Content.Tests/Fixtures/Invalid/malformed-mission.json",
                "contracts/mission.schema.json",
                "mission",
                ValidationErrorCodes.JsonMalformed
            },
            {
                "tests/DirectiveDrift.Content.Tests/Fixtures/Invalid/unknown-property.build.json",
                "contracts/build.schema.json",
                "build",
                ValidationErrorCodes.SchemaAdditionalProperties
            },
            {
                "tests/DirectiveDrift.Content.Tests/Fixtures/Invalid/invalid-action.agent-decision.json",
                "contracts/agent-decision.schema.json",
                "decision",
                ValidationErrorCodes.SchemaViolation
            },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void PreparedInvalidFixtureIsRejected(
        string fixturePath,
        string schemaPath,
        string contract,
        string expectedCode)
    {
        var json = RepositoryFiles.Read(fixturePath);
        var schema = RepositoryFiles.Read(schemaPath);
        var result = contract switch
        {
            "mission" => ContractDocumentLoader.LoadMission(json, schema).Errors,
            "build" => ContractDocumentLoader.LoadBuild(json, schema).Errors,
            "decision" => ContractDocumentLoader.LoadAgentDecision(json, schema).Errors,
            _ => throw new InvalidOperationException($"Unknown contract '{contract}'."),
        };

        Assert.Contains(result, error => error.Code == expectedCode);
    }
}
