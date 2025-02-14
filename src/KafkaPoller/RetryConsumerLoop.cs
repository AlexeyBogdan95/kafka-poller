using System.Reflection;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace KafkaPoller;

internal class RetryConsumerLoop<TMessage> : IConsumerLoop where TMessage : class
{
    private readonly IConsumer _consumer;
    private readonly KafkaPollerConfig _config;
    private readonly ILogger<IConsumerLoop> _logger;
    private readonly string _retryTopicName;

    private static readonly IDeserializer<TMessage> Deserializer = new JsonDeserializer<TMessage>();

    public RetryConsumerLoop(
        IConsumer consumer,
        KafkaPollerConfig config,
        ILogger<IConsumerLoop> logger,
        string retryTopicName)
    {
        _consumer = consumer;
        _config = config;
        _logger = logger;
        _retryTopicName = retryTopicName;
    }

    public async Task Consume(CancellationToken stoppingToken)
    {
        _logger.Log(_config.DefaultLogLevel, "Starting retry consumer for topic {Topic}", _retryTopicName);
        using var consumer = new ConsumerBuilder<string, TMessage>(
                string.IsNullOrEmpty(_config.Username) || string.IsNullOrEmpty(_config.Password)
                    ? new ConsumerConfig
                    {
                        BootstrapServers = _config.BootstrapServers,
                        GroupId = _config.ConsumerGroup,
                        EnableAutoCommit = true,
                        EnableAutoOffsetStore = false,
                        AutoOffsetReset = AutoOffsetReset.Earliest
                    }
                    : new ConsumerConfig
                    {
                        SecurityProtocol = SecurityProtocol.SaslSsl,
                        BootstrapServers = _config.BootstrapServers,
                        GroupId = _config.ConsumerGroup,
                        AutoOffsetReset = AutoOffsetReset.Latest,
                        SaslMechanism = SaslMechanism.ScramSha512,
                        SaslUsername = _config.Username,
                        SaslPassword = _config.Password,
                        EnableAutoCommit = true,
                        EnableAutoOffsetStore = false,
                        MaxPollIntervalMs = _config.MaxPollIntervalInMilliseconds
                    }
            )
            .SetValueDeserializer(Deserializer)
            .Build();

        using var redirectProducer = new ProducerBuilder<string, Redirect>(
                string.IsNullOrEmpty(_config.Username) || string.IsNullOrEmpty(_config.Password)
                ? new ProducerConfig { BootstrapServers = _config.BootstrapServers }
                : new ProducerConfig
                {
                    BootstrapServers = _config.BootstrapServers,
                    Acks = Acks.All,
                    SecurityProtocol = SecurityProtocol.SaslSsl,
                    SaslMechanism = SaslMechanism.ScramSha512,
                    SaslUsername = _config.Username,
                    SaslPassword = _config.Password
                }
            )
            .SetValueSerializer(new JsonSerializer<Redirect>())
            .Build();

        consumer.Subscribe(_retryTopicName);

        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            var cr = consumer.Consume(stoppingToken);
            if (cr?.IsPartitionEOF != false || stoppingToken.IsCancellationRequested)
                continue;

            using var msgScope = _logger.BeginScope(new Dictionary<string, object>
            {
                { "MessageKey", cr.Message.Key },
                { "CorrelationId", Guid.NewGuid() }
            });

            try
            {
                await (Task)_consumer
                    .GetType()
                    .GetMethod(
                        "Consume",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        CallingConventions.Any,
                        [cr.Message.GetType(), typeof(CancellationToken)],
                        null)!
                    .Invoke(_consumer, [cr.Message, stoppingToken]);

                var uniqueId = (string)_consumer.GetType().GetMethod(
                        "GetUniqueId",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        CallingConventions.Any,
                        [cr.Message.GetType()],
                        null)!
                    .Invoke(_consumer, [cr.Message])!;

                var tombstone = new Redirect(cr.Message.Key, uniqueId, true);
                await redirectProducer.ProduceAsync(
                    _config.RedirectTopicName,
                    new Message<string, Redirect> { Key = tombstone.Key, Value = tombstone },
                    stoppingToken);

                consumer.StoreOffset(cr);
                consumer.Commit();
            }
            catch (Exception e)
            {
                consumer.Seek(cr.TopicPartitionOffset);
                _logger.LogError(e, "Error retrying the message with key = {Key}. Retrying after {RetryInterval}ms",
                    cr.Message.Key, _config.RetryTopicsFetchIntervalInMilliseconds);
                await Task.Delay(TimeSpan.FromMilliseconds(_config.RetryTopicsFetchIntervalInMilliseconds), stoppingToken);
            }
        }

        consumer.Close();
        redirectProducer.Flush(stoppingToken);
    }
}