using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.Services;

/// <summary>
/// Listens to SignalR events and shows in-app InfoBar notifications
/// for chat messages, announcements, and task assignments.
/// </summary>
public class NotificationService
{
    private readonly SignalRService _signalR;
    private readonly ICredentialStore _credentials;
    private InfoBar? _notificationBar;
    private DispatcherQueue? _dispatcherQueue;

    public NotificationService(SignalRService signalR, ICredentialStore credentials)
    {
        _signalR = signalR;
        _credentials = credentials;
    }

    public void Attach(InfoBar bar, DispatcherQueue queue)
    {
        _notificationBar = bar;
        _dispatcherQueue = queue;

        _signalR.OnChatMessage += OnChatMessage;
        _signalR.OnAnnouncement += OnAnnouncement;
        _signalR.OnNotification += OnNotification;
    }

    private void OnChatMessage(ChatMessageDto msg)
    {
        // Don't notify for own messages
        var displayName = _credentials.GetDisplayName();
        if (msg.SenderName == displayName) return;

        ShowNotification($"New message from {msg.SenderName}", msg.Content, InfoBarSeverity.Informational);
    }

    private void OnAnnouncement(AnnouncementDto announcement)
    {
        ShowNotification($"Announcement: {announcement.Title}", announcement.Body, InfoBarSeverity.Warning);
    }

    private void OnNotification(NotificationDto notification)
    {
        ShowNotification(notification.Title, notification.Message, InfoBarSeverity.Informational);
    }

    private void ShowNotification(string title, string message, InfoBarSeverity severity)
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (_notificationBar == null) return;
            _notificationBar.Title = title;
            _notificationBar.Message = message.Length > 120 ? message[..117] + "..." : message;
            _notificationBar.Severity = severity;
            _notificationBar.IsOpen = true;
        });
    }
}
