namespace KafkaPoller;

internal interface IConsumerLoop
{
    Task Consume(CancellationToken stoppingToken);
}