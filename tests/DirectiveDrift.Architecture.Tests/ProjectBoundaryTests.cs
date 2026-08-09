using System.Xml.Linq;

namespace DirectiveDrift.Architecture.Tests;

public sealed class ProjectBoundaryTests
{
    private static readonly Dictionary<string, string[]> AllowedProjectReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["DirectiveDrift.Core"] = [],
            ["DirectiveDrift.Content"] = ["DirectiveDrift.Core"],
            ["DirectiveDrift.Application"] = ["DirectiveDrift.Core"],
            ["DirectiveDrift.AI"] = ["DirectiveDrift.Application", "DirectiveDrift.Core"],
            ["DirectiveDrift.Persistence"] = ["DirectiveDrift.Application", "DirectiveDrift.Core"],
            ["DirectiveDrift.Api"] =
            [
                "DirectiveDrift.AI",
                "DirectiveDrift.Application",
                "DirectiveDrift.Content",
                "DirectiveDrift.Persistence",
            ],
        };

    private static readonly string[] ForbiddenCoreSourceFragments =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "System.IO.",
        "System.Net.",
        "HttpClient",
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow",
        "TimeProvider",
        "System.Random",
        "Random.Shared",
        "new Random(",
        "Guid.NewGuid",
    ];

    private static readonly string[] ForbiddenCorePresentationFragments =
    [
        "VisualAnchor",
        "LabelPlacement",
        "RoomLabel",
        "VisualCoordinate",
    ];

    [Fact]
    public void SourceProjectReferencesMatchTheAllowedGraph()
    {
        var repositoryRoot = FindRepositoryRoot();
        var errors = new List<string>();

        foreach (var (projectName, _) in AllowedProjectReferences)
        {
            var projectPath = Path.Combine(repositoryRoot, "src", projectName, $"{projectName}.csproj");
            var actualReferences = ReadProjectReferences(projectPath);

            errors.AddRange(
                ProjectBoundaryRules.FindUnexpectedProjectReferences(
                    projectName,
                    actualReferences,
                    AllowedProjectReferences));

            var expectedReferences = AllowedProjectReferences[projectName].Order(StringComparer.Ordinal);
            Assert.Equal(expectedReferences, actualReferences.Order(StringComparer.Ordinal));
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void CoreHasNoPackageOrFrameworkReferences()
    {
        var projectPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "DirectiveDrift.Core",
            "DirectiveDrift.Core.csproj");
        var project = XDocument.Load(projectPath);

        var forbiddenReferences = project
            .Descendants()
            .Where(element =>
                element.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(element => element.Attribute("Include")?.Value ?? element.Name.LocalName)
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void CoreSourceDoesNotUseForbiddenRuntimeDependencies()
    {
        var coreDirectory = Path.Combine(FindRepositoryRoot(), "src", "DirectiveDrift.Core");
        var sourceFiles = Directory
            .EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Contains("obj", StringComparer.Ordinal));

        var violations = sourceFiles
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);

                return ForbiddenCoreSourceFragments
                    .Where(source.Contains)
                    .Select(fragment => $"{Path.GetRelativePath(coreDirectory, path)} contains '{fragment}'.");
            })
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CoreRuleStateContainsNoPresentationCoordinatesOrRoomLabels()
    {
        var coreDirectory = Path.Combine(FindRepositoryRoot(), "src", "DirectiveDrift.Core");
        var sourceFiles = Directory
            .EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Contains("obj", StringComparer.Ordinal));

        var violations = sourceFiles
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);

                return ForbiddenCorePresentationFragments
                    .Where(source.Contains)
                    .Select(fragment => $"{Path.GetRelativePath(coreDirectory, path)} contains '{fragment}'.");
            })
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CoreRuleRejectsAnIntentionalForbiddenReference()
    {
        var violations = ProjectBoundaryRules.FindUnexpectedProjectReferences(
            "DirectiveDrift.Core",
            ["DirectiveDrift.Persistence"],
            AllowedProjectReferences);

        Assert.Equal(
            "DirectiveDrift.Core may not reference DirectiveDrift.Persistence.",
            Assert.Single(violations));
    }

    [Fact]
    public void ContentToolsReferenceOnlyContent()
    {
        foreach (var toolName in new[]
                 {
                     "DirectiveDrift.ContentCli",
                     "DirectiveDrift.EvaluationCli",
                 })
        {
            var projectPath = Path.Combine(
                FindRepositoryRoot(),
                "tools",
                toolName,
                $"{toolName}.csproj");

            Assert.Equal(
                ["DirectiveDrift.Content"],
                ReadProjectReferences(projectPath));
        }
    }

    private static string[] ReadProjectReferences(string projectPath)
    {
        var project = XDocument.Load(projectPath);

        return project
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(Path.GetFileNameWithoutExtension)
            .Select(projectName => projectName!)
            .ToArray();
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
