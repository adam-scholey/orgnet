using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.Services;

/// <summary>
/// SignalR client for real-time tenant-scoped communication.
/// Auto-connects after login, auto-reconnects on network drops.
/// Events are dispatched to subscribers (ViewModels) via C# events.
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly ICredentialStore _credentials;
    private readonly string _hubUrl;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public event Action<TenantEventDto>? OnTenantEvent;
    public event Action<NotificationDto>? OnNotification;
    public event Action<ChatMessageDto>? OnChatMessage;
    public event Action<ChatMessageDto>? OnChatMessageEdited;
    public event Action<Guid>? OnChatMessageDeleted;
    public event Action<AnnouncementDto>? OnAnnouncement;
    public event Action<bool>? OnConnectionChanged;

    public SignalRService(ICredentialStore credentials, IConfiguration configuration)
    {
        _credentials = credentials;
        var baseUrl = configuration["OrgNet:ApiBaseUrl"] ?? "http://localhost:5100";
        _hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/orgnet";
    }

    public async Task ConnectAsync()
    {
        if (_hub != null) return;

        var token = _credentials.GetAccessToken();
        if (string.IsNullOrEmpty(token)) return;

        _hub = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        _hub.On<TenantEventDto>("ReceiveTenantEvent", evt =>
        {
            OnTenantEvent?.Invoke(evt);
        });

        _hub.On<NotificationDto>("ReceiveNotification", notification =>
        {
            OnNotification?.Invoke(notification);
        });

        _hub.On<ChatMessageDto>("ReceiveChatMessage", msg =>
        {
            OnChatMessage?.Invoke(msg);
        });

        _hub.On<ChatMessageDto>("ChatMessageEdited", msg =>
        {
            OnChatMessageEdited?.Invoke(msg);
        });

        _hub.On("ChatMessageDeleted", (Guid id) =>
        {
            OnChatMessageDeleted?.Invoke(id);
        });

        _hub.On<AnnouncementDto>("ReceiveAnnouncement", announcement =>
        {
            OnAnnouncement?.Invoke(announcement);
        });

        _hub.Reconnected += _ =>
        {
            OnConnectionChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        _hub.Reconnecting += _ =>
        {
            OnConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hub.Closed += _ =>
        {
            OnConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        try
        {
            await _hub.StartAsync();
            OnConnectionChanged?.Invoke(true);
        }
        catch
        {
            OnConnectionChanged?.Invoke(false);
        }
    }

    public async Task JoinModuleChannelAsync(string moduleId)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("JoinModuleChannel", moduleId);
    }

    public async Task LeaveModuleChannelAsync(string moduleId)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("LeaveModuleChannel", moduleId);
    }

    public async Task SendTenantEventAsync(string eventType, object payload)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("SendTenantEvent", eventType, payload);
    }

    public async Task DisconnectAsync()
    {
        if (_hub != null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
            OnConnectionChanged?.Invoke(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub != null)
            await _hub.DisposeAsync();
    }
}
