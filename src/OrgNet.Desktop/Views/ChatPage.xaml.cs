using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using OrgNet.Desktop.ViewModels;
using OrgNet.Shared.DTOs;
using Windows.System;

namespace OrgNet.Desktop.Views;

public sealed partial class ChatPage : Page
{
    private readonly ChatViewModel _vm;

    public ChatPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ChatViewModel>();
        Loaded += async (_, _) => await LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        LoadingBar.Visibility = Visibility.Visible;
        await _vm.LoadChannelsAsync();
        await _vm.LoadMessagesAsync();
        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private void UpdateUI()
    {
        ErrorBar.IsOpen = !string.IsNullOrEmpty(_vm.ErrorMessage);
        if (ErrorBar.IsOpen) ErrorBar.Message = _vm.ErrorMessage!;

        ChannelListView.ItemsSource = _vm.IsSearching ? null : _vm.Channels;
        if (!_vm.IsSearching) ChannelListView.ItemsSource = _vm.Channels;

        MessageListView.ItemsSource = _vm.IsSearching ? _vm.SearchResults : _vm.Messages;
        ChannelHeader.Text = $"#{_vm.CurrentChannel}";

        SearchBar.IsOpen = _vm.IsSearching;
        if (_vm.IsSearching) SearchBar.Message = $"{_vm.SearchResults.Count} result(s) found";
        ClearSearchButton.Visibility = _vm.IsSearching ? Visibility.Visible : Visibility.Collapsed;

        EditBar.Visibility = _vm.IsEditing ? Visibility.Visible : Visibility.Collapsed;

        // Scroll to bottom
        var items = _vm.IsSearching ? _vm.SearchResults : _vm.Messages;
        if (items.Count > 0)
            MessageListView.ScrollIntoView(items[^1]);
    }

    private async void OnChannelSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelListView.SelectedItem is ChatChannelDto channel)
        {
            LoadingBar.Visibility = Visibility.Visible;
            await _vm.SwitchChannelAsync(channel.Name);
            UpdateUI();
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnSendClicked(object sender, RoutedEventArgs e) => await SendMessageAsync();

    private async void OnMessageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) { e.Handled = true; await SendMessageAsync(); }
    }

    private async Task SendMessageAsync()
    {
        var text = MessageBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _vm.MessageText = text;
        MessageBox.Text = string.Empty;

        await _vm.SendMessageAsync();
        UpdateUI();
    }

    // ── Channel creation ──
    private void OnAddChannelClicked(object sender, RoutedEventArgs e)
    {
        NewChannelBox.Visibility = NewChannelBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (NewChannelBox.Visibility == Visibility.Visible) NewChannelBox.Focus(FocusState.Programmatic);
    }

    private async void OnNewChannelKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            var name = NewChannelBox.Text?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                LoadingBar.Visibility = Visibility.Visible;
                await _vm.CreateChannelAsync(name);
                NewChannelBox.Text = "";
                NewChannelBox.Visibility = Visibility.Collapsed;
                UpdateUI();
                LoadingBar.Visibility = Visibility.Collapsed;
            }
        }
        else if (e.Key == VirtualKey.Escape)
        {
            NewChannelBox.Visibility = Visibility.Collapsed;
        }
    }

    // ── Search ──
    private async void OnSearchClicked(object sender, RoutedEventArgs e) => await DoSearch();
    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) { e.Handled = true; await DoSearch(); }
    }

    private async Task DoSearch()
    {
        _vm.SearchQuery = SearchBox.Text?.Trim() ?? "";
        await _vm.SearchMessagesAsync();
        UpdateUI();
    }

    private void OnClearSearchClicked(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        _vm.ClearSearch();
        MessageListView.ItemsSource = _vm.Messages;
        UpdateUI();
    }

    private void OnSearchBarClosed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        SearchBox.Text = "";
        _vm.ClearSearch();
        MessageListView.ItemsSource = _vm.Messages;
        UpdateUI();
    }

    // ── Edit / Delete messages ──
    private void OnEditMessageClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ChatMessageDto msg)
        {
            _vm.StartEdit(msg);
            EditBox.Text = msg.Content;
            EditBar.Visibility = Visibility.Visible;
            EditBox.Focus(FocusState.Programmatic);
        }
    }

    private async void OnDeleteMessageClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ChatMessageDto msg)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Message",
                Content = "Are you sure you want to delete this message?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _vm.DeleteMessageAsync(msg.Id);
                UpdateUI();
            }
        }
    }

    private async void OnSaveEditClicked(object sender, RoutedEventArgs e)
    {
        _vm.EditText = EditBox.Text?.Trim() ?? "";
        await _vm.EditMessageAsync();
        UpdateUI();
    }

    private void OnCancelEditClicked(object sender, RoutedEventArgs e)
    {
        _vm.CancelEdit();
        EditBar.Visibility = Visibility.Collapsed;
    }

    private async void OnEditKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _vm.EditText = EditBox.Text?.Trim() ?? "";
            await _vm.EditMessageAsync();
            UpdateUI();
        }
        else if (e.Key == VirtualKey.Escape)
        {
            _vm.CancelEdit();
            EditBar.Visibility = Visibility.Collapsed;
        }
    }

    // ── Right-click context menu ──
    private void OnMessageRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        // Right-click handled by the inline edit/delete buttons
    }
}
