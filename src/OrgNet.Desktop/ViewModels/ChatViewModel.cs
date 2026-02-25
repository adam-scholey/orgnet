using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;
    private readonly SignalRService _signalR;

    public ChatViewModel(OrgNetApiClient api, SignalRService signalR)
    {
        _api = api;
        _signalR = signalR;

        _signalR.OnChatMessage += msg =>
        {
            if (msg.Channel == CurrentChannel && Messages.All(m => m.Id != msg.Id))
                Messages.Add(msg);
        };

        _signalR.OnChatMessageEdited += msg =>
        {
            for (int i = 0; i < Messages.Count; i++)
                if (Messages[i].Id == msg.Id) { Messages[i] = msg; break; }
        };

        _signalR.OnChatMessageDeleted += id =>
        {
            var toRemove = Messages.FirstOrDefault(m => m.Id == id);
            if (toRemove != null) Messages.Remove(toRemove);
        };
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _currentChannel = "general";
    [ObservableProperty] private string _messageText = string.Empty;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private ChatMessageDto? _selectedMessage;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editText = string.Empty;

    public ObservableCollection<ChatChannelDto> Channels { get; } = new();
    public ObservableCollection<ChatMessageDto> Messages { get; } = new();
    public ObservableCollection<ChatMessageDto> SearchResults { get; } = new();

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

    [ObservableProperty] private bool _hasMoreMessages = true;

    [RelayCommand]
    public async Task LoadOlderMessagesAsync()
    {
        if (!HasMoreMessages || Messages.Count == 0) return;

        ErrorMessage = null;
        try
        {
            var oldest = Messages.First().SentAt;
            var older = await _api.GetChatMessagesAsync(CurrentChannel + $"&before={oldest:O}", 50);
            if (older != null && older.Count > 0)
            {
                for (int i = older.Count - 1; i >= 0; i--)
                    Messages.Insert(0, older[i]);
            }
            else
            {
                HasMoreMessages = false;
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
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

    [RelayCommand]
    public async Task EditMessageAsync()
    {
        if (SelectedMessage == null || string.IsNullOrWhiteSpace(EditText)) return;

        ErrorMessage = null;
        try
        {
            var result = await _api.EditChatMessageAsync(SelectedMessage.Id, EditText.Trim());
            if (result != null)
            {
                var idx = -1;
                for (int i = 0; i < Messages.Count; i++)
                    if (Messages[i].Id == result.Id) { idx = i; break; }

                if (idx >= 0) Messages[idx] = result;
                CancelEdit();
            }
            else ErrorMessage = _api.LastError ?? "Failed to edit message";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    public async Task DeleteMessageAsync(Guid messageId)
    {
        ErrorMessage = null;
        try
        {
            var result = await _api.DeleteChatMessageAsync(messageId);
            if (result)
            {
                var toRemove = Messages.FirstOrDefault(m => m.Id == messageId);
                if (toRemove != null) Messages.Remove(toRemove);
            }
            else ErrorMessage = _api.LastError ?? "Failed to delete message";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    public async Task SearchMessagesAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            IsSearching = false;
            SearchResults.Clear();
            return;
        }

        IsSearching = true;
        ErrorMessage = null;
        try
        {
            var results = await _api.GetChatMessagesAsync(CurrentChannel, 100);
            SearchResults.Clear();
            if (results != null)
            {
                var query = SearchQuery.ToLowerInvariant();
                foreach (var m in results.Where(m => m.Content.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || m.SenderName.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    SearchResults.Add(m);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    public void StartEdit(ChatMessageDto message)
    {
        SelectedMessage = message;
        EditText = message.Content;
        IsEditing = true;
    }

    public void CancelEdit()
    {
        SelectedMessage = null;
        EditText = string.Empty;
        IsEditing = false;
    }

    public void ClearSearch()
    {
        SearchQuery = string.Empty;
        IsSearching = false;
        SearchResults.Clear();
    }

    public async Task CreateChannelAsync(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName)) return;

        ErrorMessage = null;
        try
        {
            // Send a system message to create the channel (channels auto-create on first message)
            var result = await _api.SendChatMessageAsync(new SendMessageRequest(channelName.Trim().ToLowerInvariant().Replace(' ', '-'), $"Channel created"));
            if (result != null)
            {
                await LoadChannelsAsync();
                await SwitchChannelAsync(result.Channel);
            }
            else ErrorMessage = _api.LastError ?? "Failed to create channel";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    public async Task SwitchChannelAsync(string channel)
    {
        CurrentChannel = channel;
        ClearSearch();
        await LoadMessagesAsync();
    }
}
