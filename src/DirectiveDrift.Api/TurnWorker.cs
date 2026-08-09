using DirectiveDrift.Application;

namespace DirectiveDrift.Api;

public sealed class TurnWorker(
    TurnOperationProcessor processor,
    ILogger<TurnWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> WorkerIterationFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(4001, nameof(WorkerIterationFailed)),
            "Turn worker iteration failed without advancing state.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await processor.ProcessNextAsync(stoppingToken);
                if (!processed && !await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                WorkerIterationFailed(logger, exception);
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
        }
    }
}
