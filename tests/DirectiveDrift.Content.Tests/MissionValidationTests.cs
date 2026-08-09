using System.Text.Json.Nodes;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Tests;

public sealed class MissionValidationTests
{
    private static readonly string MissionSchema =
        RepositoryFiles.Read("contracts/mission.schema.json");

    [Fact]
    public void CanonicalMissionLoadsWithTypedIndexes()
    {
        var result = LoadCanonical();

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(new MissionId("cold-start"), result.Mission!.MissionId);
        Assert.True(result.Mission.Rooms.ContainsKey(new RoomId("landing-bay")));
        Assert.True(result.Mission.Agents.ContainsKey(new AgentId("kite")));
        Assert.True(
            result.Mission.BriefingCards.ContainsKey(new BriefingCardId("power-contract")));
        Assert.Equal(11, result.Mission.Rooms.Count);
        Assert.Equal(11, result.Mission.Variants.Count);
    }

    [Fact]
    public void DuplicateIdsAreRejectedWithAStableCode()
    {
        var mission = CanonicalObject();
        var rooms = mission["rooms"]!.AsArray();
        rooms[1]!["roomId"] = rooms[0]!["roomId"]!.GetValue<string>();

        var result = MissionLoader.Load(mission.ToJsonString(), MissionSchema);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.ContentDuplicateId);
    }

    [Fact]
    public void ExactlyTwoContentDefinedAgentIdsAreAccepted()
    {
        var mission = CanonicalObject();
        mission["agents"]!.AsArray()[0]!["agentId"] = "rook";
        mission["agents"]!.AsArray()[0]!["label"] = "Rook";
        mission["agents"]!.AsArray()[1]!["agentId"] = "lark";
        mission["agents"]!.AsArray()[1]!["label"] = "Lark";
        mission["connections"]!.AsArray()[11]!["allowedAgentIds"]!.AsArray()[0] = "rook";
        mission["devices"]!["generator"]!["repairAgentId"] = "lark";

        var result = MissionLoader.Load(mission.ToJsonString(), MissionSchema);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(2, result.Mission!.Agents.Count);
        Assert.Equal("Rook", result.Mission.Agents[new AgentId("rook")].Label);
        Assert.Equal("Lark", result.Mission.Agents[new AgentId("lark")].Label);
    }

    [Fact]
    public void UnresolvedReferencesAreRejectedWithAStableCode()
    {
        var mission = CanonicalObject();
        mission["connections"]!.AsArray()[0]!["fromRoomId"] = "missing-room";

        var result = MissionLoader.Load(mission.ToJsonString(), MissionSchema);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.ContentUnresolvedReference
                && error.Path == "/connections/0/fromRoomId");
    }

    [Fact]
    public void OutOfRangePatrolIndexIsRejected()
    {
        var mission = CanonicalObject();
        var mutation = mission["variants"]!.AsArray()[0]!["mutations"]!.AsArray()[1]!;
        mutation["startIndex"] = 50;

        var result = MissionLoader.Load(mission.ToJsonString(), MissionSchema);

        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.ContentInvalidReference
                && error.Path == "/variants/0/mutations/1/startIndex");
    }

    [Fact]
    public void RawSvgIsRejectedBeforeItCanEnterPresentationData()
    {
        var mission = CanonicalObject();
        mission["rawSvg"] = "<svg><path d=\"M0 0\" /></svg>";

        var result = MissionLoader.Load(mission.ToJsonString(), MissionSchema);

        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.ContentRawPresentationMarkup);
    }

    [Fact]
    public void RawCssIsRejectedEvenInsideSchemaPermittedText()
    {
        var mission = CanonicalObject();
        mission["briefingCards"]!.AsArray()[0]!["text"] = ".room { fill: red; }";

        var result = MissionLoader.Load(mission.ToJsonString(), MissionSchema);

        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.ContentRawPresentationMarkup);
    }

    private static MissionLoadResult LoadCanonical()
        => MissionLoader.Load(
            RepositoryFiles.Read("content/missions/cold-start/mission.json"),
            MissionSchema);

    private static JsonObject CanonicalObject()
        => RepositoryFiles.ReadObject("content/missions/cold-start/mission.json");
}
