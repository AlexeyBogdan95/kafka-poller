namespace KafkaPoller;

using Confluent.Kafka;
using Microsoft.Extensions.Logging;

internal class RedirectConsumerLoop : IConsumerLoop
{
    private readonly RedirectController _redirectController;
    private readonly KafkaPollerConfig _config;
    private readonly ILogger<RedirectConsumerLoop> _logger;

    private static readonly IDeserializer<Redirect> Deserializer = new JsonDeserializer<Redirect>();

    public RedirectConsumerLoop(
        RedirectController redirectController,
        KafkaPollerConfig config,
        ILogger<RedirectConsumerLoop> logger)
    {
        _redirectController = redirectController;
        _config = config;
        _logger = logger;
    }

    public async Task Consume(CancellationToken stoppingToken)
    {
        _logger.Log(_config.DefaultLogLevel, "Starting redirect consumer for topic {Topic}", _config.RedirectTopicName);
        using var consumer = new ConsumerBuilder<string, Redirect>(
                string.IsNullOrEmpty(_config.Username) || string.IsNullOrEmpty(_config.Password)
                    ? new ConsumerConfig
                    {
                        BootstrapServers = _config.BootstrapServers,
                        GroupId = _config.ConsumerGroup + Guid.NewGuid(),
                        EnableAutoCommit = false,
                        EnableAutoOffsetStore = false,
                        AutoOffsetReset = AutoOffsetReset.Earliest,
                        EnablePartitionEof = true
                    }
                    : new ConsumerConfig
                    {
                        SecurityProtocol = SecurityProtocol.SaslSsl,
                        BootstrapServers = _config.BootstrapServers,
                        GroupId = _config.ConsumerGroup + Guid.NewGuid(),
                        AutoOffsetReset = AutoOffsetReset.Earliest,
                        SaslMechanism = SaslMechanism.ScramSha512,
                        SaslUsername = _config.Username,
                        SaslPassword = _config.Password,
                        EnableAutoCommit = false,
                        EnableAutoOffsetStore = false,
                        EnablePartitionEof = true
                    }
            )
            .SetValueDeserializer(Deserializer)
            .Build();

        consumer.Subscribe(_config.RedirectTopicName);

        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            var cr = consumer.Consume(stoppingToken);

            if (!_redirectController.Initialized && cr?.IsPartitionEOF == true)
            {
                _redirectController.Initialized = true;
                _logger.Log(_config.DefaultLogLevel, "Redirect Controller initialized successfully");
            }

            if (cr?.IsPartitionEOF != false || stoppingToken.IsCancellationRequested)
                continue;

            var redirect = cr.Message.Value;
            _redirectController.Apply(redirect);

            _logger.Log(
                _config.DefaultLogLevel,
                "Redirect event consumed. Key = {Key}, UniqueId = {UniqueId}, IsTombstone = {IsTombstone}",
                redirect.Key, redirect.UniqueId, redirect.IsTombstone);
        }

        consumer.Close();
    }
}