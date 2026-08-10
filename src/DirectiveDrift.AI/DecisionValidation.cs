using System.Text.Json;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Core.Decisions;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.AI;

public sealed record DecisionValidationResult(
    ProposedDecision? Decision,
    ProviderAttemptStatus Status,
    string DiagnosticCode,
    bool Repairable);

public static class DecisionValidation
{
    private static readonly HashSet<string> RequiredProperties =
        ["schemaVersion", "actionId", "message", "rationale", "memory"];

    public static DecisionValidationResult Validate(
        string json,
        AgentTurnContext context,
        AgentId otherAgentId)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return Invalid(ProviderAttemptStatus.MalformedJson, "malformed-json", true);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.EnumerateObject().Select(value => value.Name).ToHashSet().SetEquals(RequiredProperties)
                || !TryString(root, "schemaVersion", out var schemaVersion)
                || !string.Equals(schemaVersion, "1", StringComparison.Ordinal)
                || !TryString(root, "actionId", out var actionId)
                || !TryString(root, "rationale", out var rationale)
                || !TryString(root, "memory", out var memory))
            {
                return Invalid(ProviderAttemptStatus.InvalidSchema, "invalid-schema", true);
            }

            if (HasDisallowedControls(rationale) || rationale.Length is < 1 or > 180)
            {
                return Invalid(ProviderAttemptStatus.InvalidDecision, "rationale-invalid", false);
            }

            if (HasDisallowedControls(memory) || memory.Length > context.Limits.MemoryCharacters)
            {
                return Invalid(ProviderAttemptStatus.InvalidDecision, "memory-invalid", false);
            }

            var legalActionId = new ActionId(actionId);
            if (!context.LegalActions.Any(value => value.ActionId == legalActionId))
            {
                return Invalid(ProviderAttemptStatus.InvalidDecision, "action-not-legal", false);
            }

            var messageElement = root.GetProperty("message");
            string? message = null;
            if (messageElement.ValueKind != JsonValueKind.Null)
            {
                if (messageElement.ValueKind != JsonValueKind.Object
                    || !messageElement.EnumerateObject().Select(value => value.Name).ToHashSet()
                        .SetEquals(["toAgentId", "text"])
                    || !TryString(messageElement, "toAgentId", out var recipient)
                    || !TryString(messageElement, "text", out message))
                {
                    return Invalid(ProviderAttemptStatus.InvalidSchema, "message-schema-invalid", true);
                }

                if (string.Equals(recipient, context.Self.AgentId.Value, StringComparison.Ordinal)
                    || message.Length is < 1
                    || message.Length > context.Limits.MessageCharacters
                    || HasDisallowedControls(message))
                {
                    return Invalid(ProviderAttemptStatus.InvalidDecision, "message-invalid", false);
                }

                if (!string.Equals(recipient, otherAgentId.Value, StringComparison.Ordinal))
                {
                    return Invalid(ProviderAttemptStatus.InvalidDecision, "recipient-invalid", false);
                }
            }

            return new DecisionValidationResult(
                new ProposedDecision(
                    legalActionId,
                    message is null ? null : NormalizeLineEndings(message),
                    NormalizeLineEndings(rationale),
                    NormalizeLineEndings(memory)),
                ProviderAttemptStatus.Accepted,
                "accepted",
                false);
        }
    }

    private static bool TryString(JsonElement parent, string name, out string value)
    {
        if (parent.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool HasDisallowedControls(string value) =>
        value.Any(character => character is < ' ' and not '\n' and not '\r' and not '\t' || character == '\u007f');

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private static DecisionValidationResult Invalid(
        ProviderAttemptStatus status,
        string code,
        bool repairable) => new(null, status, code, repairable);
}
