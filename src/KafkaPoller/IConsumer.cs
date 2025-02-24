using Confluent.Kafka;

namespace KafkaPoller;

/// <summary>
/// It's used for IoC purposes only.
/// </summary>
/// <remarks>
/// Please don't use it
/// </remarks>
public interface IConsumer;

public interface IConsumer<T> :IConsumer
{
    Task Consume(Message<string, T> message, CancellationToken cancellationToken);
    string GetUniqueId(Message<string, T> message);
}