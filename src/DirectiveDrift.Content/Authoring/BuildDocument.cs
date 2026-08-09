namespace DirectiveDrift.Content.Authoring;

public sealed record BuildDocument(
    string SchemaVersion,
    string BuildId,
    string MissionId,
    string Name,
    int Version,
    string SharedDoctrine,
    IReadOnlyDictionary<string, AgentBuildDocument> Agents,
    string? Hypothesis);

public sealed record AgentBuildDocument(
    string RoleOrder,
    IReadOnlyList<string> BriefingCardIds,
    string ModuleId);
