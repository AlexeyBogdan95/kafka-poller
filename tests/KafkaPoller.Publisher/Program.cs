﻿using Confluent.Kafka;
using KafkaPoller;
using KafkaPoller.Publisher;

using var producerMessageOne = new ProducerBuilder<string, MessageOne>(new ProducerConfig { BootstrapServers = "localhost:29092" })
    .SetValueSerializer(new JsonSerializer<MessageOne>())
    .Build();
for (var i = 0; i < 100; i++)
{
    await producerMessageOne.ProduceAsync("test.topic1",
        new Message<string, MessageOne>
        {
            Key = Guid.NewGuid().ToString(),
            Value = new MessageOne
            {
                Val = i
            },
            Headers = [new Header("id", [Convert.ToByte(i)])]
        },
        CancellationToken.None
    );
}


using var producerMessageTwo = new ProducerBuilder<string, MessageTwo>(new ProducerConfig { BootstrapServers = "localhost:29092" })
    .SetValueSerializer(new JsonSerializer<MessageTwo>())
    .Build();

for (var i = 0; i < 200; i++)
{
    await producerMessageTwo.ProduceAsync("test.topic2",
        new Message<string, MessageTwo>
        {
            Key = Guid.NewGuid().ToString(),
            Value = new MessageTwo
            {
                Text = $"Message{i}"
            },
            Headers = [new Header("id", [Convert.ToByte(i)])]
        },
        CancellationToken.None
    );
}

