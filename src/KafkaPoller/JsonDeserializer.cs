using System.Text.Json;
using Confluent.Kafka;

namespace KafkaPoller;

public class JsonDeserializer<T> : IDeserializer<T> where T : class
{
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull)
        {
            throw new NullReferenceException();
        }

        return JsonSerializer.Deserialize<T>(data)!;
    }
}