using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KafkaPoller;

public static class KafkaPollerConfiguration
{
    public static IServiceCollection AddKafkaPoller(
        this IServiceCollection services,
        KafkaPollerConfig config,
        KafkaConsumerConfig[] consumerConfigs)
    {
        services.AddSingleton(config);
        services.AddSingleton<RedirectController>();
        services.AddSingleton<IConsumerLoop, RedirectConsumerLoop>();
        foreach (var consumerConfig in consumerConfigs)
        {
            services.AddSingleton(typeof(IConsumerLoop),s =>
            {
                var consumerLoopType = typeof(ConsumerLoop<>).MakeGenericType(consumerConfig.MessageType);
                return Activator.CreateInstance(
                    consumerLoopType,
                    s.GetRequiredService<RedirectController>(),
                    s.GetRequiredService<KafkaPollerConfig>(),
                    s.GetRequiredService<ILogger<IConsumerLoop>>(),
                    s.GetRequiredService<IConsumer>(),
                    consumerConfig.TopicName,
                    consumerConfig.RetryTopicName
                )!;
            });

            if (!string.IsNullOrEmpty(consumerConfig.RetryTopicName))
            {
                services.AddSingleton(typeof(IConsumerLoop),s =>
                {
                    var consumerLoopType = typeof(RetryConsumerLoop<>).MakeGenericType(consumerConfig.MessageType);
                    return Activator.CreateInstance(
                        consumerLoopType,
                        s.GetRequiredService<IConsumer>(),
                        s.GetRequiredService<KafkaPollerConfig>(),
                        s.GetRequiredService<ILogger<IConsumerLoop>>(),
                        consumerConfig.RetryTopicName
                    )!;
                });
            }
        }

        services.AddHostedService<ConsumerLoopService>();
        return services;
    }
}