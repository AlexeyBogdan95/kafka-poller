using KafkaPoller;
using KafkaPoller.Consumer.Consumers;
using KafkaPoller.Publisher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using MessageOne = KafkaPoller.Consumer.Consumers.MessageOne;
using MessageTwo = KafkaPoller.Consumer.Consumers.MessageTwo;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder();
    builder.Services
        .AddSingleton<IConsumer, ConsumerOne>()
        .AddSingleton<IConsumer, ConsumerTwo>()
        .AddKafkaPoller(new KafkaPollerConfig
        {
            ConsumerGroup = "test.consumer",
            BootstrapServers = "localhost:29092"
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
                },
                new KafkaConsumerConfig
                {
                    TopicName = "test.topic3",
                    MessageType = typeof(MessageThree),
                    ConsumerType = typeof(ConsumerTwo)
                }
            ]);

    builder.Build().Run();
    return 0;
}
catch (Exception e)
{
    Console.WriteLine(e);
    return 1;
}

