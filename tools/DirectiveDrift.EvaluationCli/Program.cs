using System.Collections.Immutable;
using DirectiveDrift.Content.Contracts;
using DirectiveDrift.Content.Evaluation;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Content.Validation;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    var parsed = Parse(arguments);
    if (parsed is null)
    {
        Console.Error.WriteLine(
            "Usage: DirectiveDrift.EvaluationCli --build <build.json> "
            + "--provider-mode scripted --matrix <tutorial|practice|certification|pinned|all|ids> "
            + "--repetitions <count>");
        return 2;
    }

    var repositoryRoot = FindRepositoryRoot();
    if (repositoryRoot is null)
    {
        Console.Error.WriteLine("content.file-read-failed / Repository root could not be located.");
        return 2;
    }

    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;

    try
    {
        var missionResult = MissionLoader.Load(
            await ReadAsync(repositoryRoot, "content/missions/cold-start/mission.json", cancellation.Token),
            await ReadAsync(repositoryRoot, "contracts/mission.schema.json", cancellation.Token));
        if (!missionResult.IsValid)
        {
            WriteErrors(missionResult.Errors);
            return 1;
        }

        var mission = missionResult.Mission!;
        var catalogResult = ColdStartVariantCatalogLoader.Load(
            mission,
            await ReadAsync(
                repositoryRoot,
                "content/missions/cold-start/server/certification-variants.json",
                cancellation.Token),
            await ReadAsync(
                repositoryRoot,
                $"contracts/{ContractSchemaFiles.CertificationVariants}",
                cancellation.Token));
        if (!catalogResult.IsValid)
        {
            WriteErrors(catalogResult.Errors);
            return 1;
        }

        var buildResult = ContractDocumentLoader.LoadBuild(
            await File.ReadAllTextAsync(Path.GetFullPath(parsed.BuildPath), cancellation.Token),
            await ReadAsync(
                repositoryRoot,
                $"contracts/{ContractSchemaFiles.Build}",
                cancellation.Token));
        if (!buildResult.IsValid)
        {
            WriteErrors(buildResult.Errors);
            return 1;
        }

        var matrix = EvaluationMatrixSelector.Select(catalogResult.Catalog!, parsed.Matrix);
        var report = ScriptedEvaluationRunner.Run(
            mission,
            catalogResult.Catalog!,
            buildResult.Document!,
            new EvaluationRequest(EvaluationProviderMode.Scripted, matrix, parsed.Repetitions));

        foreach (var run in report.Runs)
        {
            Console.WriteLine(
                $"variant={run.VariantId} repetition={run.Repetition} status={run.Status} "
                + $"turns={run.Turns} damage={run.DamageTaken} fallbacks={run.FallbackDecisions} "
                + $"failure={run.FailureSignature ?? "none"}");
        }

        Console.WriteLine(
            $"build={report.BuildId} provider=scripted runs={report.Runs.Length} "
            + $"successes={report.Successes} failures={report.Failures} "
            + $"fallbacks={report.FallbackDecisions}/{report.TotalAgentTurns}");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Evaluation canceled.");
        return 2;
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }
}

static CliArguments? Parse(string[] arguments)
{
    if (arguments.Length != 8)
    {
        return null;
    }

    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index < arguments.Length; index += 2)
    {
        if (!arguments[index].StartsWith("--", StringComparison.Ordinal)
            || !values.TryAdd(arguments[index], arguments[index + 1]))
        {
            return null;
        }
    }

    if (!values.TryGetValue("--build", out var buildPath)
        || !values.TryGetValue("--provider-mode", out var providerMode)
        || !string.Equals(providerMode, "scripted", StringComparison.Ordinal)
        || !values.TryGetValue("--matrix", out var matrix)
        || !values.TryGetValue("--repetitions", out var repetitionsText)
        || !int.TryParse(repetitionsText, out var repetitions)
        || repetitions <= 0)
    {
        return null;
    }

    return new CliArguments(buildPath, matrix, repetitions);
}

static async Task<string> ReadAsync(
    string repositoryRoot,
    string relativePath,
    CancellationToken cancellationToken) => await File.ReadAllTextAsync(
        Path.Combine(repositoryRoot, relativePath),
        cancellationToken);

static void WriteErrors(IEnumerable<ValidationError> errors)
{
    foreach (var error in errors)
    {
        Console.Error.WriteLine($"{error.Code} {error.Path} {error.Message}");
    }
}

static string? FindRepositoryRoot()
{
    var current = new DirectoryInfo(Environment.CurrentDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "DirectiveDrift.sln")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return null;
}

internal sealed record CliArguments(string BuildPath, string Matrix, int Repetitions);
