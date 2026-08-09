using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Authoring;

public sealed record BuildDocument(
    string SchemaVersion,
    string BuildId,
    string MissionId,
    string Name,
    int Version,
    string SharedDoctrine,
    IReadOnlyDictionary<AgentId, AgentBuildDocument> Agents,
    string? Hypothesis);

public sealed record AgentBuildDocument(
    string RoleOrder,
    IReadOnlyList<string> BriefingCardIds,
    string ModuleId);
