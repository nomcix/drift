using DirectiveDrift.Content.Contracts;
using DirectiveDrift.Content.Loading;
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

        Console.WriteLine(
            $"Valid mission '{result.Mission!.MissionId}' ({result.Mission.Authoring.ContentVersion}).");
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
