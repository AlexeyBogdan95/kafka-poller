using Microsoft.Extensions.Logging;

namespace KafkaPoller;

public class KafkaPollerConfig
{

    /// <summary>
    /// Consumer Group Name
    /// Required
    /// </summary>
    public required string ConsumerGroup { get; set; }

    /// <summary>
    /// Initial list of brokers as host:port.
    /// Default: ''
    /// Confluent reference:
    /// </summary>
    public required string BootstrapServers { get; set; }


    /// <summary>
    /// SASL username for use with the PLAIN and SASL-SCRAM-.. mechanisms
    /// Default: ''
    /// </summary>
    public string? Username { get; set; } = string.Empty;

    /// <summary>
    /// SASL password for use with the PLAIN and SASL-SCRAM-.. mechanism
    /// Default: ''
    /// </summary>
    public string? Password { get; set; } = string.Empty;


    /// <summary>
    /// Maximum allowed time to call between calls to consume messages
    /// Default: 300000
    /// </summary>
    public int? MaxPollIntervalInMilliseconds { get; set; } = 300_000;

    /// <summary>
    /// Time in milliseconds between calls to kafka to consume messages from retry queues
    /// Default: 30000
    /// </summary>
    public int RetryTopicsFetchIntervalInMilliseconds { get; set; } = 30_000;

    /// <summary>
    /// Log level for common log messages
    /// Default: LogLevel.Trace
    /// </summary>
    public LogLevel DefaultLogLevel { get; set; } = LogLevel.Trace;

    /// <summary>
    /// Redirect topic name
    /// Default {consumer_name}.redirect
    /// </summary>
    public string? RedirectTopic { get; set; }

    internal string RedirectTopicName => RedirectTopic ?? $"{ConsumerGroup}.redirect";
}