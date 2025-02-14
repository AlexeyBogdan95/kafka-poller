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
    public Task Consume(Message<string, T> message, CancellationToken cancellationToken);

    public string GetUniqueId(Message<string, T> message)
    {
        return message.Key;
    }
}