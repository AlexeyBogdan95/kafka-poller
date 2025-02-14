using Confluent.Kafka;

namespace KafkaPoller.Consumer.Consumers;

public class MessageOne
{
    public required int Val { get; set; }
}

public class MessageTwo
{
    public required string Text { get; set; }
}

public class ConsumerOne: IConsumer<MessageOne>, IConsumer<MessageTwo>
{
    public Task Consume(Message<string, MessageOne> message, CancellationToken token)
    {
        Console.WriteLine($"MessageOne: {message.Value.Val}");
        return Task.CompletedTask;
    }

    public string GetUniqueId(Message<string, MessageOne> message)
    {
        return message.Key;
    }

    public Task Consume(Message<string, MessageTwo> message, CancellationToken token)
    {
        Console.WriteLine($"MessageTwo: {message.Value.Text}");
        return Task.CompletedTask;
    }


    public string GetUniqueId(Message<string, MessageTwo> message)
    {
        return message.Key;
    }
}