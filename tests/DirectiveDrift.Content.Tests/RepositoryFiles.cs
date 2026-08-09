using System.Text.Json.Nodes;

namespace DirectiveDrift.Content.Tests;

internal static class RepositoryFiles
{
    public static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

    public static JsonObject ReadObject(string relativePath)
    {
        return JsonNode.Parse(Read(relativePath))?.AsObject()
            ?? throw new InvalidOperationException($"'{relativePath}' is not a JSON object.");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DirectiveDrift.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the Directive Drift repository root.");
    }
}
