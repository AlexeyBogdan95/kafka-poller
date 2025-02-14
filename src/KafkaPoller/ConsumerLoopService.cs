using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KafkaPoller;

internal class ConsumerLoopService : BackgroundService
{
    private readonly ILogger<ConsumerLoopService> _logger;
    private readonly KafkaPollerConfig _config;
    private readonly IEnumerable<IConsumerLoop> _consumers;

    public ConsumerLoopService(
        ILogger<ConsumerLoopService> logger,
        IEnumerable<IConsumerLoop> consumers,
        KafkaPollerConfig config)
    {
        _logger = logger;
        _consumers = consumers;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Log(_config.DefaultLogLevel, "Starting {ConsumersCount} consumers.", _consumers.Count());
        await await Task.WhenAny(_consumers.Select(loop => loop.Consume(stoppingToken)));
        _logger.Log(_config.DefaultLogLevel,"Finishing consumers");
    }
}