using System.Globalization;
using System.Text;
using MediatR;
using Axpo;

namespace PowerPosition.Worker.Features.PowerPosition;

public class ExtractPowerPositionHandler(
    IPowerService powerService,
    IConfiguration configuration,
    ILogger<ExtractPowerPositionHandler> logger) : IRequestHandler<ExtractPowerPositionCommand>
{
    public async Task Handle(ExtractPowerPositionCommand request, CancellationToken cancellationToken)
    {
        var extractDate = request.Date.Date;
        logger.LogInformation("Handling Power Position Extraction for Date: {Date}", extractDate);

        var stopWatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var trades = await powerService.GetTradesAsync(extractDate);
            var tradesList = trades.ToList();

            logger.LogInformation("Fetched {Count} trades. Duration: {Elapsed}ms", tradesList.Count, stopWatch.ElapsedMilliseconds);

            var aggregatedVolumes = new Dictionary<int, double>();

            for (int i = 1; i <= 24; i++)
            {
                aggregatedVolumes[i] = 0.0;
            }

            foreach (var trade in tradesList)
            {
                if (trade is { Periods: { Length: > 0 } periods })
                {
                    foreach (var period in periods)
                    {
                        if (aggregatedVolumes.ContainsKey(period.Period))
                        {
                            aggregatedVolumes[period.Period] += period.Volume;
                        }
                    }
                }
            }

            var totalVolume = aggregatedVolumes.Values.Sum();
            logger.LogInformation("Aggregation complete. Total Daily Volume: {TotalVolume}. Time elapsed: {Elapsed}ms", totalVolume, stopWatch.ElapsedMilliseconds);

            var sb = new StringBuilder();
            sb.AppendLine("Local Time,Volume");

            var startTime = extractDate.AddDays(-1).AddHours(23);

            for (int i = 1; i <= aggregatedVolumes.Count; i++)
            {
                var volume = aggregatedVolumes[i];
                var timeSlot = startTime.AddHours(i - 1);

                sb.AppendLine($"{timeSlot:HH:mm},{volume}");
            }

            string outputDir = configuration.GetValue<string>("PowerPosition:OutputDirectory")
                               ?? Path.Combine(Directory.GetCurrentDirectory(), "output");

            if (!Directory.Exists(outputDir))
            {
                logger.LogInformation("Creating output directory: {OutputDir}", outputDir);
                Directory.CreateDirectory(outputDir);
            }

            var londonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
            var nowLondon = TimeZoneInfo.ConvertTime(DateTime.Now, londonTimeZone);

            string fileName = $"PowerPosition_{nowLondon:yyyyMMdd_HHmm}.csv";
            string filePath = Path.Combine(outputDir, fileName);

            await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);

            stopWatch.Stop();
            logger.LogInformation("Generated Report: {FilePath}. Total Duration: {Elapsed}ms", filePath, stopWatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopWatch.Stop();
            logger.LogError(ex, "Failed to extract power position. Duration: {Elapsed}ms", stopWatch.ElapsedMilliseconds);
            throw;
        }
    }
}
