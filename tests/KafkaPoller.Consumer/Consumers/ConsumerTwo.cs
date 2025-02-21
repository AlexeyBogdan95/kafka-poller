using Confluent.Kafka;
using KafkaPoller.Publisher;

namespace KafkaPoller.Consumer.Consumers;

public class ConsumerTwo: IConsumer<MessageThree>
{
    public Task Consume(Message<string, MessageThree> message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Message three: {message.Value.Value}");
        return Task.CompletedTask;
    }
}