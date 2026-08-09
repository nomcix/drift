using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Core.Scoring;

public sealed record ProgressSummary(
    bool PowerRestored,
    bool ArchiveOpened,
    bool RecorderRecovered,
    int ActiveAgentsAtExtraction);

public sealed record ScoreResult(
    bool IsRanked,
    int? Score,
    string ScoreVersion,
    ProgressSummary Progress);

public static class ScoreCalculator
{
    public static ScoreResult Calculate(RunState terminalState)
    {
        var progress = new ProgressSummary(
            terminalState.Generator.Condition == GeneratorCondition.Online,
            terminalState.ArchiveGateOpen,
            terminalState.Recorder.Condition == RecorderCondition.Extracted,
            terminalState.Agents.Count(agent =>
                agent.Status == AgentStatus.Active
                && agent.RoomId == terminalState.ExtractionRoomId));

        if (terminalState.Status != RunStatus.Succeeded || terminalState.Score.Assisted)
        {
            return new ScoreResult(false, null, terminalState.Mission.ScoreVersion, progress);
        }

        var unusedTurns = terminalState.Rules.TurnLimit - terminalState.Turn;
        var remainingHealth = terminalState.Agents.Sum(agent => agent.Health);
        var remainingModuleCharges = terminalState.Agents.Sum(
            agent => agent.Module.ChargesRemaining);

        var score = 1000
            + (35 * unusedTurns)
            + (50 * remainingHealth)
            + (20 * terminalState.Communication.RemainingMessages)
            + (25 * remainingModuleCharges)
            + (terminalState.Score.FailedConsoleActivations == 0 ? 75 : 0)
            + (terminalState.Score.InterruptedMajorRepairs == 0 ? 75 : 0);

        return new ScoreResult(true, score, terminalState.Mission.ScoreVersion, progress);
    }
}
