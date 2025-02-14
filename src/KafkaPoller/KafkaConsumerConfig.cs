namespace KafkaPoller;

public class KafkaConsumerConfig
{
    public required string TopicName { get; set; }
    public string? RetryTopicName { get; set; }
    public required Type MessageType { get; set; }
    public required Type ConsumerType { get; set; }
}