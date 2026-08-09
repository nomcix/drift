namespace DirectiveDrift.Content.Validation;

public sealed record ValidationError(string Code, string Path, string Message);

public sealed record ValidationReport(IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static ValidationReport Valid { get; } = new([]);
}

public sealed record DocumentLoadResult<T>(
    T? Document,
    IReadOnlyList<ValidationError> Errors)
    where T : class
{
    public bool IsValid => Document is not null && Errors.Count == 0;
}
