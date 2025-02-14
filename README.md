# Kafka Poller

Kafka Poller is a .NET library for consuming messages from a Kafka topic with built-in support for a retry pattern. The library provides mechanisms for handling message redirection and tombstoning, ensuring reliable and flexible message processing.

## Features

- **Kafka Consumer**: Efficiently consume messages from a Kafka topic.
- **Retry Pattern**: Automatically retry failed messages with configurable retry strategies.
- **Message Redirection**: Move messages to different topics for alternative processing.
- **Tombstone Handling**: Support for tombstone messages to mark records as deleted.
- **High Performance**: Optimized for fast and reliable message processing.
- **Pattern 4 Support**: Ensures messages are processed in order by pausing consumption until failed messages are retried and successfully processed.

## Installation

Install the package via NuGet:

```sh
 dotnet add package KafkaPoller
```

## Usage

### Basic Consumer Setup

```csharp
using KafkaPoller;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddKafkaPoller(new KafkaPollerConfig
        {
            ConsumerGroup = "test.consumer",
            BootstrapServers = "localhost:29092",
            Username = "username",
            Password = "password",
            RedirectTopic = "test.topic.retry",
        }, [
          new KafkaConsumerConfig
          {
            TopicName = "test.topic1",
            MessageType = typeof(MessageOne),
            ConsumerType = typeof(ConsumerOne),
            RetryTopicName = "test.consumer.test.topic1.retry"
          },
          new KafkaConsumerConfig
          {
            TopicName = "test.topic2",
            MessageType = typeof(MessageTwo),
            ConsumerType = typeof(ConsumerOne)
          }
        ]);
    });

await builder.RunConsoleAsync();
```

### Custom Message Handler

```csharp
using KafkaPoller.Abstractions;
using System;
using System.Threading.Tasks;

public class ConsumerOne: IConsumer<MessageOne>, IConsumer<MessageTwo>
{
    public Task Consume(Message<string, MessageOne> message, CancellationToken token)
    {
        Console.WriteLine($"MessageOne: {message.Value.Val}");
        return Task.CompletedTask;
    }

    public Task Consume(Message<string, MessageTwo> message, CancellationToken token)
    {
        Console.WriteLine($"MessageTwo: {message.Value.Text}");
        return Task.CompletedTask;
    }

    public string GetUniqueId(Message<string, MessageOne> message)
    {
        return message.Key;
    }

    public string GetUniqueId(Message<string, MessageTwo> message)
    {
        return message.Key;
    }
}
```

### Configuring Retries and Redirection

Kafka Poller allows automatic redirection of failed messages to a retry topic.

### Handling Tombstone Messages

Tombstone messages (messages with `null` values) can be processed or ignored based on configuration:

```csharp
options.TombstoneHandling = true;
```

## Configuration Options

| Option                                  | Description                                    |
|-----------------------------------------|--------------------------------|
| `ConsumerGroup`                         | Kafka consumer group name (Required). |
| `BootstrapServers`                      | Kafka server addresses (comma-separated). |
| `Username`                              | SASL username for authentication. |
| `Password`                              | SASL password for authentication. |
| `MaxPollIntervalInMilliseconds`         | Maximum time allowed between poll calls (Default: 300000). |
| `RetryTopicsFetchIntervalInMilliseconds`| Time interval to consume messages from retry queues (Default: 30000). |
| `DefaultLogLevel`                       | Logging level for messages (Default: LogLevel.Trace). |
| `RedirectTopic`                         | Redirect topic name (Default: `{consumer_name}.redirect`). |

## Contributing

Contributions are welcome! Feel free to open an issue or submit a pull request.

## License

MIT License. See `LICENSE` for more details.

