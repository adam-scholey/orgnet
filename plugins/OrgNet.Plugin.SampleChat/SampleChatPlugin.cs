using Microsoft.Extensions.DependencyInjection;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Plugin.SampleChat;

/// <summary>
/// Sample plugin demonstrating the IOrgNetPlugin contract.
/// Registers a simple in-memory chat service that tenants can enable.
/// </summary>
public class SampleChatPlugin : IOrgNetPlugin
{
    public string ModuleId => "OrgNet.Chat";
    public string Name => "Team Chat";
    public string Description => "Real-time team messaging with channels and direct messages";
    public string Version => "1.0.0";
    public string? IconUrl => null;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ChatService>();
    }

    public Task InitialiseAsync(IServiceProvider serviceProvider)
    {
        // In production: run migrations, seed default channels, etc.
        return Task.CompletedTask;
    }
}

/// <summary>Simple in-memory chat service for demonstration</summary>
public class ChatService
{
    private readonly List<ChatMessage> _messages = new();

    public void PostMessage(Guid tenantId, Guid userId, string channel, string content)
    {
        _messages.Add(new ChatMessage(Guid.NewGuid(), tenantId, userId, channel, content, DateTime.UtcNow));
    }

    public IReadOnlyList<ChatMessage> GetMessages(Guid tenantId, string channel, int limit = 50)
    {
        return _messages
            .Where(m => m.TenantId == tenantId && m.Channel == channel)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .Reverse()
            .ToList();
    }
}

public record ChatMessage(Guid Id, Guid TenantId, Guid UserId, string Channel, string Content, DateTime Timestamp);
