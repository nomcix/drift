using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Validation;

public static class BuildReferenceValidator
{
    public static ValidationReport Validate(BuildDocument build, ValidatedMission mission)
    {
        var errors = new List<ValidationError>();

        if (new MissionId(build.MissionId) != mission.MissionId)
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentUnresolvedReference,
                    "/missionId",
                    $"Mission ID '{build.MissionId}' does not match the loaded mission."));
        }

        var buildAgentIds = build.Agents.Keys
            .Select(agentId => new AgentId(agentId))
            .ToHashSet();
        var missionAgentIds = mission.Agents.Keys.ToHashSet();

        foreach (var agentId in buildAgentIds
                     .Where(agentId => !missionAgentIds.Contains(agentId))
                     .OrderBy(agentId => agentId.Value, StringComparer.Ordinal))
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentUnresolvedReference,
                    $"/agents/{agentId.Value}",
                    $"Agent ID '{agentId}' does not resolve in the selected mission."));
        }

        foreach (var agentId in missionAgentIds
                     .Where(agentId => !buildAgentIds.Contains(agentId))
                     .OrderBy(agentId => agentId.Value, StringComparer.Ordinal))
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentInvalidReference,
                    "/agents",
                    $"The build is missing selected mission agent ID '{agentId}'."));
        }

        foreach (var (agentId, agentBuild) in build.Agents.OrderBy(
                     entry => entry.Key,
                     StringComparer.Ordinal))
        {
            ValidateAgentBuild(agentBuild, $"/agents/{agentId}", mission, errors);
        }

        return new ValidationReport(
            errors
                .OrderBy(error => error.Path, StringComparer.Ordinal)
                .ThenBy(error => error.Message, StringComparer.Ordinal)
                .ToArray());
    }

    private static void ValidateAgentBuild(
        AgentBuildDocument agentBuild,
        string path,
        ValidatedMission mission,
        List<ValidationError> errors)
    {
        for (var index = 0; index < agentBuild.BriefingCardIds.Count; index++)
        {
            var cardId = new BriefingCardId(agentBuild.BriefingCardIds[index]);

            if (!mission.BriefingCards.ContainsKey(cardId))
            {
                errors.Add(
                    new ValidationError(
                        ValidationErrorCodes.ContentUnresolvedReference,
                        $"{path}/briefingCardIds/{index}",
                        $"Briefing card ID '{cardId}' does not resolve."));
            }
        }

        var moduleId = new ModuleId(agentBuild.ModuleId);

        if (!mission.Modules.ContainsKey(moduleId))
        {
            errors.Add(
                new ValidationError(
                    ValidationErrorCodes.ContentUnresolvedReference,
                    $"{path}/moduleId",
                    $"Module ID '{moduleId}' does not resolve."));
        }
    }
}
