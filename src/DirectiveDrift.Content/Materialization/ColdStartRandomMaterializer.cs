using DirectiveDrift.Content.Authoring;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Core.Model;
using DirectiveDrift.Core.Random;

namespace DirectiveDrift.Content.Materialization;

public sealed record RandomPracticeMaterialization(
    ulong Seed,
    VariantDocument Variant,
    MaterializationResult Result);

public static class ColdStartRandomMaterializer
{
    private const ulong Stream = 0x434f4c4453544152UL;

    public static RandomPracticeMaterialization Materialize(
        ValidatedMission mission,
        ColdStartVariantCatalog catalog,
        ulong seed,
        IReadOnlyDictionary<AgentId, SupportModule>? modulesByAgent = null)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.PracticeVariants.Length == 0)
        {
            throw new InvalidOperationException("The safe practice catalogue is empty.");
        }

        var draw = Pcg32.Next(Pcg32.Seed(seed, Stream)).Value;
        var source = catalog.PracticeVariants[(int)(draw % (uint)catalog.PracticeVariants.Length)];
        var variant = source with
        {
            VariantId = $"cs-practice-random-{seed:x}",
            Label = $"Seeded Practice {seed}",
            Visibility = VariantVisibility.Practice,
        };

        return new RandomPracticeMaterialization(
            seed,
            variant,
            ColdStartMissionMaterializer.Materialize(mission, variant, modulesByAgent));
    }
}
