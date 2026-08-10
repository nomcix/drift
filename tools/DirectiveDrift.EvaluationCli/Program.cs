using System.Collections.Immutable;
using DirectiveDrift.AI;
using DirectiveDrift.Application.Models;
using DirectiveDrift.Content.Contracts;
using DirectiveDrift.Content.Evaluation;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Content.Validation;
using DirectiveDrift.EvaluationCli;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    var parsed = Parse(arguments);
    if (parsed is null)
    {
        Console.Error.WriteLine(
            "Usage: DirectiveDrift.EvaluationCli --build <build.json> "
            + "--provider-mode <scripted|live> --matrix <tutorial|practice|certification|pinned|all|ids> "
            + "--repetitions <count> [--output <report.json> --spend-cap-micros <count>]");
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
        if (parsed.ProviderMode == "live")
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.Error.WriteLine("OPENAI_API_KEY is required for live evaluation.");
                return 2;
            }

            using var http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/") };
            var profile = ProviderProfiles.OpenAi;
            var provider = new StructuredDecisionProvider(
                profile,
                new OpenAiResponsesTransport(http, apiKey));
            var live = await LiveEvaluationRunner.RunAsync(
                mission,
                catalogResult.Catalog!,
                buildResult.Document!,
                matrix,
                parsed.Repetitions,
                provider,
                parsed.SpendCapMicros!.Value,
                parsed.OutputPath!,
                cancellation.Token);
            Console.WriteLine(
                $"build={live.BuildId} provider=live runs={live.Runs.Length} "
                + $"successes={live.Successes} failures={live.Runs.Length - live.Successes} "
                + $"invalid={live.InvalidDecisions}/{live.AgentTurns} costMicros={live.CostMicros}");
            return 0;
        }

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
    if (arguments.Length < 8 || arguments.Length % 2 != 0)
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
        || providerMode is not ("scripted" or "live")
        || !values.TryGetValue("--matrix", out var matrix)
        || !values.TryGetValue("--repetitions", out var repetitionsText)
        || !int.TryParse(repetitionsText, out var repetitions)
        || repetitions <= 0)
    {
        return null;
    }

    string? outputPath = null;
    int? spendCapMicros = null;
    if (providerMode == "live")
    {
        if (!values.TryGetValue("--output", out outputPath)
            || !values.TryGetValue("--spend-cap-micros", out var spendCapText)
            || !int.TryParse(spendCapText, out var spendCap)
            || spendCap <= 0)
        {
            return null;
        }

        spendCapMicros = spendCap;
    }

    var expectedKeys = providerMode == "live" ? 6 : 4;
    if (values.Count != expectedKeys)
    {
        return null;
    }

    return new CliArguments(buildPath, providerMode, matrix, repetitions, outputPath, spendCapMicros);
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

internal sealed record CliArguments(
    string BuildPath,
    string ProviderMode,
    string Matrix,
    int Repetitions,
    string? OutputPath,
    int? SpendCapMicros);
