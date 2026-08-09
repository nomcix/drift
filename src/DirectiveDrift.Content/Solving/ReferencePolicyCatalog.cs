using System.Collections.Immutable;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Solving;

public enum ReferencePolicyFamily
{
    ReconCourier,
    EngineerCourier,
    BeaconWindow,
}

public sealed record ReferencePolicyFamilyDefinition(
    ReferencePolicyFamily Family,
    string Name,
    ImmutableArray<VariantId> ApplicableVariantIds);

public static class ReferencePolicyCatalog
{
    public static ImmutableArray<ReferencePolicyFamilyDefinition> Families { get; } =
    [
        new(
            ReferencePolicyFamily.ReconCourier,
            "Recon courier",
            VariantIds("cs-practice-01", "cs-practice-02", "cs-cert-01", "cs-cert-02")),
        new(
            ReferencePolicyFamily.EngineerCourier,
            "Engineer courier",
            VariantIds("cs-practice-04", "cs-cert-01")),
        new(
            ReferencePolicyFamily.BeaconWindow,
            "Beacon window",
            VariantIds("cs-practice-01", "cs-practice-05", "cs-cert-03", "cs-cert-06")),
    ];

    public static IReadOnlyDictionary<AgentId, SupportModule> CreateModules(
        RunDefinition moduleFreeDefinition,
        ReferencePolicyFamily family)
    {
        ArgumentNullException.ThrowIfNull(moduleFreeDefinition);

        var recon = moduleFreeDefinition.Agents.Single(
            agent => agent.Archetype == AgentArchetype.Recon);
        var engineer = moduleFreeDefinition.Agents.Single(
            agent => agent.Archetype == AgentArchetype.Engineer);

        return family switch
        {
            ReferencePolicyFamily.ReconCourier => new Dictionary<AgentId, SupportModule>
            {
                [recon.AgentId] = SupportModule.CargoClamp,
                [engineer.AgentId] = SupportModule.RapidRepairKit,
            },
            ReferencePolicyFamily.EngineerCourier => new Dictionary<AgentId, SupportModule>
            {
                [recon.AgentId] = SupportModule.DecoyBeacon,
                [engineer.AgentId] = SupportModule.CargoClamp,
            },
            ReferencePolicyFamily.BeaconWindow => new Dictionary<AgentId, SupportModule>
            {
                [recon.AgentId] = SupportModule.DecoyBeacon,
                [engineer.AgentId] = SupportModule.HazardShield,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };
    }

    public static ReferencePolicyOptions CreateOptions(
        RunDefinition definition,
        ReferencePolicyFamily family,
        int maximumTurns = 17,
        int minimumSyncTurn = 1)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var recon = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Recon);
        var engineer = definition.Agents.Single(agent => agent.Archetype == AgentArchetype.Engineer);
        var alphaAgentId = definition.ConsoleAlpha.InitialCondition == ConsoleCondition.Damaged
            ? engineer.AgentId
            : recon.AgentId;

        return family switch
        {
            ReferencePolicyFamily.ReconCourier => new ReferencePolicyOptions(
                alphaAgentId,
                recon.AgentId,
                maximumTurns,
                minimumSyncTurn,
                RequireNoDamage: false),
            ReferencePolicyFamily.EngineerCourier => new ReferencePolicyOptions(
                alphaAgentId,
                engineer.AgentId,
                maximumTurns,
                minimumSyncTurn,
                RequireNoDamage: false),
            ReferencePolicyFamily.BeaconWindow => new ReferencePolicyOptions(
                alphaAgentId,
                engineer.AgentId,
                maximumTurns,
                minimumSyncTurn,
                RequireNoDamage: true,
                RequiredConsumedModule: SupportModule.DecoyBeacon),
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };
    }

    private static ImmutableArray<VariantId> VariantIds(params string[] values) =>
        values.Select(value => new VariantId(value)).ToImmutableArray();
}
