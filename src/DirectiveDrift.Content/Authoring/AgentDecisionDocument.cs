namespace DirectiveDrift.Content.Authoring;

public sealed record AgentDecisionDocument(
    string SchemaVersion,
    string ActionId,
    AgentMessageDocument? Message,
    string Rationale,
    string Memory);

public sealed record AgentMessageDocument(string ToAgentId, string Text);
