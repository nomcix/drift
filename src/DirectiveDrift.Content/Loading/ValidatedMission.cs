using System.Collections.Frozen;
using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Loading;

public sealed class ValidatedMission
{
    public ValidatedMission(MissionDocument authoring)
    {
        Authoring = authoring;
        MissionId = new MissionId(authoring.MissionId);
        Agents = authoring.Agents.ToFrozenDictionary(
            agent => new AgentId(agent.AgentId));
        Rooms = authoring.Rooms.ToFrozenDictionary(
            room => new RoomId(room.RoomId));
        Connections = authoring.Connections.ToFrozenDictionary(
            connection => new ConnectionId(connection.ConnectionId));
        BriefingCards = authoring.BriefingCards.ToFrozenDictionary(
            card => new BriefingCardId(card.CardId));
        Modules = authoring.Modules.ToFrozenDictionary(
            module => new ModuleId(module.ModuleId));
        Objectives = authoring.Objectives.ToFrozenDictionary(
            objective => new ObjectiveId(objective.ObjectiveId));
        Variants = authoring.Variants.ToFrozenDictionary(
            variant => new VariantId(variant.VariantId));
    }

    public MissionDocument Authoring { get; }

    public MissionId MissionId { get; }

    public IReadOnlyDictionary<AgentId, AgentDocument> Agents { get; }

    public IReadOnlyDictionary<RoomId, RoomDocument> Rooms { get; }

    public IReadOnlyDictionary<ConnectionId, ConnectionDocument> Connections { get; }

    public IReadOnlyDictionary<BriefingCardId, BriefingCardDocument> BriefingCards { get; }

    public IReadOnlyDictionary<ModuleId, ModuleDocument> Modules { get; }

    public IReadOnlyDictionary<ObjectiveId, ObjectiveDocument> Objectives { get; }

    public IReadOnlyDictionary<VariantId, VariantDocument> Variants { get; }
}

public sealed record MissionLoadResult(
    ValidatedMission? Mission,
    IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Mission is not null && Errors.Count == 0;
}
