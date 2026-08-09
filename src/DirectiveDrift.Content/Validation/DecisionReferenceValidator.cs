using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Validation;

public static class DecisionReferenceValidator
{
    public static ValidationReport Validate(
        AgentDecisionDocument decision,
        ValidatedMission mission)
    {
        if (decision.Message is null
            || mission.Agents.ContainsKey(new AgentId(decision.Message.ToAgentId)))
        {
            return ValidationReport.Valid;
        }

        return new ValidationReport(
        [
            new ValidationError(
                ValidationErrorCodes.ContentUnresolvedReference,
                "/message/toAgentId",
                $"Agent ID '{decision.Message.ToAgentId}' does not resolve."),
        ]);
    }
}
