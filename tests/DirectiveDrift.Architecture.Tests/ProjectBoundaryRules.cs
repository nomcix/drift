namespace DirectiveDrift.Architecture.Tests;

internal static class ProjectBoundaryRules
{
    public static IReadOnlyList<string> FindUnexpectedProjectReferences(
        string projectName,
        IEnumerable<string> actualReferences,
        IReadOnlyDictionary<string, string[]> allowedReferences)
    {
        if (!allowedReferences.TryGetValue(projectName, out var allowed))
        {
            return [$"Unknown source project '{projectName}'."];
        }

        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);

        return actualReferences
            .Where(reference => !allowedSet.Contains(reference))
            .Order(StringComparer.Ordinal)
            .Select(reference => $"{projectName} may not reference {reference}.")
            .ToArray();
    }
}
