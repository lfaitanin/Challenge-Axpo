using MediatR;
using PowerPosition.Worker.Features.PowerPosition;

namespace PowerPosition.Worker;

public class Worker(
    ILogger<Worker> logger,
    IMediator mediator,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        double intervalMinutes = configuration.GetValue<double>("PowerPosition:ScheduleIntervalMinutes");
        logger.LogInformation("Worker running at: {time} with interval {interval} minutes", DateTimeOffset.Now, intervalMinutes);

        await TriggerExtractionAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TriggerExtractionAsync(stoppingToken);
        }
    }

    private async Task TriggerExtractionAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Initiating Power Position Extraction...");

            var command = new ExtractPowerPositionCommand(DateTime.Now);
            await mediator.Send(command, cancellationToken);

            logger.LogInformation("Extraction completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during extraction.");
        }
    }
}
