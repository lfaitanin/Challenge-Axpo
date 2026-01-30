using Axpo;
using Polly;
using Polly.Retry;

namespace PowerPosition.Worker.Services;

public class ResilientPowerService : IPowerService
{
    private readonly IPowerService _innerService;
    private readonly ILogger<ResilientPowerService> _logger;
    private readonly ResiliencePipeline _pipeline;

    public ResilientPowerService(IPowerService innerService, ILogger<ResilientPowerService> logger)
    {
        _innerService = innerService;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(), // Handle all exceptions, or specialize if needed
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Retry attempt {RetryCount} after error: {Message}. Delaying {Delay}ms",
                        args.AttemptNumber,
                        args.Outcome.Exception?.Message,
                        args.RetryDelay.TotalMilliseconds);

                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public Task<IEnumerable<PowerTrade>> GetTradesAsync(DateTime date)
    {
        return _pipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Calling PowerService.GetTradesAsync for date {Date}", date);
            return await _innerService.GetTradesAsync(date);
        }).AsTask();
    }
    public IEnumerable<PowerTrade> GetTrades(DateTime date)
    {
        return _pipeline.Execute(() =>
        {
            _logger.LogDebug("Calling synchronous PowerService.GetTrades for date {Date}", date);
            return _innerService.GetTrades(date);
        });
    }
}
