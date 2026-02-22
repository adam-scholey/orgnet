using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;

    public ChatViewModel(OrgNetApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _currentChannel = "general";
    [ObservableProperty] private string _messageText = string.Empty;

    public ObservableCollection<ChatChannelDto> Channels { get; } = new();
    public ObservableCollection<ChatMessageDto> Messages { get; } = new();

    [RelayCommand]
    public async Task LoadChannelsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var channels = await _api.GetChatChannelsAsync();
            Channels.Clear();
            if (channels != null)
                foreach (var c in channels) Channels.Add(c);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task LoadMessagesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var messages = await _api.GetChatMessagesAsync(CurrentChannel);
            Messages.Clear();
            if (messages != null)
                foreach (var m in messages) Messages.Add(m);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText)) return;

        ErrorMessage = null;
        try
        {
            var result = await _api.SendChatMessageAsync(new SendMessageRequest(CurrentChannel, MessageText.Trim()));
            if (result != null)
            {
                Messages.Add(result);
                MessageText = string.Empty;
            }
            else ErrorMessage = _api.LastError ?? "Failed to send message";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    public async Task SwitchChannelAsync(string channel)
    {
        CurrentChannel = channel;
        await LoadMessagesAsync();
    }
}
