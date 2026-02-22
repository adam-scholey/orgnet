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

        ChannelListView.ItemsSource = _vm.Channels;
        MessageListView.ItemsSource = _vm.Messages;
        ChannelHeader.Text = $"#{_vm.CurrentChannel}";

        // Scroll to bottom
        if (_vm.Messages.Count > 0)
            MessageListView.ScrollIntoView(_vm.Messages[^1]);
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

    private async void OnSendClicked(object sender, RoutedEventArgs e)
    {
        await SendMessageAsync();
    }

    private async void OnMessageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            await SendMessageAsync();
        }
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
}
