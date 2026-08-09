using System.Collections.Immutable;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Evaluation;

public static class EvaluationMatrixSelector
{
    public static ImmutableArray<VariantId> Select(
        ColdStartVariantCatalog catalog,
        string matrix)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(matrix);

        return matrix switch
        {
            "tutorial" => Require(catalog, ["cs-practice-01"]),
            "practice" => catalog.PracticeVariants
                .Select(variant => new VariantId(variant.VariantId))
                .ToImmutableArray(),
            "certification" => catalog.CertificationVariants
                .Select(variant => new VariantId(variant.VariantId))
                .ToImmutableArray(),
            "pinned" => Require(
                catalog,
                [
                    "cs-practice-01",
                    "cs-practice-02",
                    "cs-practice-03",
                    "cs-practice-04",
                    "cs-practice-05",
                    "cs-cert-01",
                    "cs-cert-02",
                    "cs-cert-03",
                ]),
            "all" => catalog.AllFixedVariants
                .Select(variant => new VariantId(variant.VariantId))
                .ToImmutableArray(),
            _ => Require(
                catalog,
                matrix.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
        };
    }

    private static ImmutableArray<VariantId> Require(
        ColdStartVariantCatalog catalog,
        IEnumerable<string> values)
    {
        var result = values
            .Select(value => new VariantId(value))
            .Distinct()
            .ToImmutableArray();

        if (result.Length == 0 || result.Any(variantId => catalog.Find(variantId) is null))
        {
            throw new ArgumentException("The evaluation matrix contains an unknown variant ID.");
        }

        return result;
    }
}
