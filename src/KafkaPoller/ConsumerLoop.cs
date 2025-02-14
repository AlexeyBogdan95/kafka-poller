using System.Reflection;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Polly;

namespace KafkaPoller;

internal class ConsumerLoop<TMessage> : IConsumerLoop where TMessage : class
{
    private readonly RedirectController _redirectController;
    private readonly KafkaPollerConfig _config;
    private readonly ILogger<IConsumerLoop> _logger;
    private readonly IConsumer _consumer;
    private readonly string _topicName;
    private readonly string? _retryTopicName;

    public ConsumerLoop(
        RedirectController redirectController,
        KafkaPollerConfig config,
        ILogger<IConsumerLoop> logger,
        IConsumer consumer,
        string topicName,
        string? retryTopicName)
    {
        _redirectController = redirectController;
        _config = config;
        _logger = logger;
        _consumer = consumer;
        _topicName = topicName;
        _retryTopicName = retryTopicName;
    }

    private static readonly IDeserializer<TMessage> Deserializer = new JsonDeserializer<TMessage>();

    public async Task Consume(CancellationToken stoppingToken)
    {
        var hasRetryTopic = !string.IsNullOrEmpty(_retryTopicName);
        _logger.Log(_config.DefaultLogLevel, "Starting consumer for topic {TopicName}", _topicName);

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
                    ? new ProducerConfig
                    {
                        BootstrapServers = _config.BootstrapServers
                    }
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

        using var retryProducer = new ProducerBuilder<string, TMessage>(
                string.IsNullOrEmpty(_config.Username) || string.IsNullOrEmpty(_config.Password)
                    ? new ProducerConfig
                    {
                        BootstrapServers = _config.BootstrapServers
                    }
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
            .SetValueSerializer(new JsonSerializer<TMessage>())
            .Build();

        consumer.Subscribe(_topicName);

        do
        {
            _logger.Log(_config.DefaultLogLevel,"Waiting for redirect controller to initialize...");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        } while (!_redirectController.Initialized);

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

            if (hasRetryTopic && _redirectController.IsOnRetryFlow(cr.Message.Key))
            {
                _logger.Log(
                    _config.DefaultLogLevel,
                    "The key {Key} is already on retry flow. Redirecting the message to retry flow",
                    cr.Message.Key);
                await RedirectToRetryFlow(cr.Message, retryProducer, redirectProducer, stoppingToken);
                continue;
            }

            try
            {
                await (Task)_consumer.GetType().GetMethod(
                        "Consume",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        CallingConventions.Any,
                        [cr.Message.GetType(), typeof(CancellationToken)],
                        null)!
                    .Invoke(_consumer, [
                        cr.Message,
                        stoppingToken
                    ]);
            }
            catch (OperationCanceledException e)
            {
                 _logger.LogError(e, "Consumer was cancelled");
                 continue;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to handle a message with key = {Key}", cr.Message.Key);
                if (hasRetryTopic)
                {
                    await RedirectToRetryFlow(cr.Message, retryProducer, redirectProducer, stoppingToken);
                }
                else
                {
                    consumer.Seek(cr.TopicPartitionOffset);
                }
            }
            consumer.StoreOffset(cr);
        }

        consumer.Close();
        redirectProducer.Flush(stoppingToken);
        retryProducer.Flush(stoppingToken);
    }

    private async Task RedirectToRetryFlow(
        Message<string, TMessage> message,
        IProducer<string, TMessage> retryProducer,
        IProducer<string, Redirect> redirectProducer,
        CancellationToken stoppingToken = default)
    {
        var uniqueId = (string)_consumer.GetType().GetMethod(
                "GetUniqueId",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                CallingConventions.Any,
                [message.GetType()],
                null)!
            .Invoke(_consumer, [message])!;

        var redirect = new Redirect(message.Key, uniqueId, false);
        var redirectMessage = new Message<string, Redirect>
        {
            Key = message.Key,
            Value = redirect
        };
        _redirectController.Apply(redirect);

        var policy = Policy
            .Handle<Exception>()
            .WaitAndRetryForeverAsync(
                (i, _) => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, i), 600)),
                (e, span, _) => _logger.LogError(e,
                    "Error redirecting message to retry flow, key = {Key}. Retrying after {TimeInterval}s",
                    message.Key, span.TotalSeconds));

        await Task.WhenAll(
            policy.ExecuteAsync(token => retryProducer.ProduceAsync(_retryTopicName, message, token), stoppingToken),
            policy.ExecuteAsync(token => redirectProducer.ProduceAsync(_config.RedirectTopicName, redirectMessage, token), stoppingToken));
    }
}