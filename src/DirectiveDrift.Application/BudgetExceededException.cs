namespace DirectiveDrift.Application;

public sealed class BudgetExceededException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
