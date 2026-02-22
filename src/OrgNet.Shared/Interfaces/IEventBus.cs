namespace OrgNet.Shared.Interfaces;

/// <summary>
/// Internal event bus for decoupled communication between modules.
/// Backend uses SignalR + Redis pub/sub; Desktop uses in-process messaging.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(string channel, T payload, CancellationToken ct = default);
    void Subscribe<T>(string channel, Func<T, Task> handler);
    void Unsubscribe(string channel);
}
