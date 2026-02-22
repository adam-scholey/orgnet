using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrgNet.Shared.Interfaces;
using StackExchange.Redis;

namespace OrgNet.Infrastructure.Eventing;

/// <summary>
/// Redis-backed event bus for cross-process pub/sub.
/// Used for real-time tenant events when multiple API instances are running.
/// Falls back gracefully if Redis is unavailable (logs warning, no-ops).
/// 
/// Architectural decision: Redis pub/sub over RabbitMQ for simplicity.
/// OrgNet doesn't need durable message queues at MVP — pub/sub is fire-and-forget
/// for real-time UI updates. Upgrade path to RabbitMQ/MassTransit is straightforward.
/// </summary>
public class RedisEventBus : IEventBus, IDisposable
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ISubscriber? _subscriber;
    private readonly ILogger<RedisEventBus> _logger;
    private readonly Dictionary<string, Action> _unsubscribeActions = new();

    public RedisEventBus(IConnectionMultiplexer? redis, ILogger<RedisEventBus> logger)
    {
        _redis = redis;
        _subscriber = redis?.GetSubscriber();
        _logger = logger;
    }

    public async Task PublishAsync<T>(string channel, T payload, CancellationToken ct = default)
    {
        if (_subscriber == null)
        {
            _logger.LogWarning("Redis not available — event on channel '{Channel}' dropped", channel);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload);
            await _subscriber.PublishAsync(RedisChannel.Literal(channel), json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event on channel '{Channel}'", channel);
        }
    }

    public void Subscribe<T>(string channel, Func<T, Task> handler)
    {
        if (_subscriber == null)
        {
            _logger.LogWarning("Redis not available — cannot subscribe to '{Channel}'", channel);
            return;
        }

        var redisChannel = RedisChannel.Literal(channel);
        _subscriber.Subscribe(redisChannel, async (_, message) =>
        {
            try
            {
                var payload = JsonSerializer.Deserialize<T>((string)message!);
                if (payload != null)
                    await handler(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling event on channel '{Channel}'", channel);
            }
        });

        _unsubscribeActions[channel] = () => _subscriber.Unsubscribe(redisChannel);
    }

    public void Unsubscribe(string channel)
    {
        if (_unsubscribeActions.TryGetValue(channel, out var unsub))
        {
            unsub();
            _unsubscribeActions.Remove(channel);
        }
    }

    public void Dispose()
    {
        foreach (var unsub in _unsubscribeActions.Values)
            unsub();
        _unsubscribeActions.Clear();
    }
}
