using DirectiveDrift.Content.Validation;
using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Content.Materialization;

public sealed record MaterializationResult(
    RunDefinition? Definition,
    IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Definition is not null && Errors.Count == 0;
}
