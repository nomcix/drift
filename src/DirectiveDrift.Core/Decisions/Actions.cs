using System.Collections.Immutable;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Core.Decisions;

public enum LegalActionKind
{
    Move,
    Scan,
    RepairGenerator,
    ContinueGeneratorRepair,
    RepairConsole,
    ActivateConsole,
    PickupRecorder,
    DeployDecoyBeacon,
    Wait,
}

public enum RuleTargetKind
{
    None,
    Room,
    Device,
    MissionItem,
}

public readonly record struct RuleTarget(RuleTargetKind Kind, string Value)
{
    public static RuleTarget None => new(RuleTargetKind.None, string.Empty);
}

public sealed record LegalAction(
    ActionId ActionId,
    LegalActionKind Kind,
    RuleTarget Target);

public sealed record LegalActionSet(
    AgentId AgentId,
    ImmutableArray<LegalAction> Actions)
{
    public LegalAction? Find(ActionId actionId) => Actions.FirstOrDefault(
        action => action.ActionId == actionId);
}

public sealed record ProposedDecision(
    ActionId ActionId,
    string? Message,
    string Rationale,
    string Memory,
    DecisionFallbackReason? ForcedFallbackReason = null);

public enum DecisionFallbackReason
{
    Missing,
    IllegalAction,
    MessageTooLong,
    RationaleTooLong,
    MemoryTooLong,
}

public sealed record ResolvedDecision(
    AgentId AgentId,
    LegalAction Action,
    string? Message,
    string Rationale,
    string Memory,
    DecisionFallbackReason? FallbackReason)
{
    public bool UsedFallback => FallbackReason is not null;
}
