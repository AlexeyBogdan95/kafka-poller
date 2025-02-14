using Confluent.Kafka;
using KafkaPoller;
using KafkaPoller.Consumer.Consumers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder();
    builder.Services
        .AddSingleton<IConsumer, ConsumerOne>()
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

