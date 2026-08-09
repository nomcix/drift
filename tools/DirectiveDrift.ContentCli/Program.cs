using DirectiveDrift.Content.Contracts;
using DirectiveDrift.Content.Loading;
using DirectiveDrift.Content.Materialization;
using DirectiveDrift.Content.Validation;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    if (arguments.Length != 2
        || !string.Equals(arguments[0], "validate", StringComparison.Ordinal))
    {
        Console.Error.WriteLine(
            "Usage: DirectiveDrift.ContentCli validate <mission.json>");
        return 2;
    }

    var missionPath = Path.GetFullPath(arguments[1]);
    var schemaPath = FindRepositoryFile(Path.Combine("contracts", ContractSchemaFiles.Mission));

    if (schemaPath is null)
    {
        Console.Error.WriteLine("content.file-read-failed / Mission schema could not be located.");
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
        var result = await MissionFileLoader
            .LoadAsync(missionPath, schemaPath, cancellation.Token);

        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"{error.Code} {error.Path} {error.Message}");
            }

            return 1;
        }

        var mission = result.Mission!;
        if (string.Equals(mission.MissionId.Value, "cold-start", StringComparison.Ordinal))
        {
            var certificationPath = FindRepositoryFile(
                Path.Combine(
                    "content",
                    "missions",
                    "cold-start",
                    "server",
                    "certification-variants.json"));
            var certificationSchemaPath = FindRepositoryFile(
                Path.Combine("contracts", ContractSchemaFiles.CertificationVariants));
            if (certificationPath is null || certificationSchemaPath is null)
            {
                Console.Error.WriteLine(
                    "content.file-read-failed / Certification fixture or schema could not be located.");
                return 2;
            }

            var catalogResult = ColdStartVariantCatalogLoader.Load(
                mission,
                await File.ReadAllTextAsync(certificationPath, cancellation.Token),
                await File.ReadAllTextAsync(certificationSchemaPath, cancellation.Token));
            if (!catalogResult.IsValid)
            {
                WriteErrors(catalogResult.Errors);
                return 1;
            }

            var validation = ColdStartContentValidator.Validate(mission, catalogResult.Catalog!);
            if (!validation.IsValid)
            {
                WriteErrors(validation.Errors);
                return 1;
            }

            Console.WriteLine(
                $"Valid mission '{mission.MissionId}' ({mission.Authoring.ContentVersion}); "
                + $"{validation.Proofs.Length} variants proven.");
            return 0;
        }

        Console.WriteLine($"Valid mission '{mission.MissionId}' ({mission.Authoring.ContentVersion}).");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Validation canceled.");
        return 2;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }
}

static void WriteErrors(IEnumerable<ValidationError> errors)
{
    foreach (var error in errors)
    {
        Console.Error.WriteLine($"{error.Code} {error.Path} {error.Message}");
    }
}

static string? FindRepositoryFile(string relativePath)
{
    var current = new DirectoryInfo(Environment.CurrentDirectory);

    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, relativePath);

        if (File.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return null;
}
