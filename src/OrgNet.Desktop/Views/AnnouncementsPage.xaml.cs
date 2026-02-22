using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using OrgNet.Desktop.ViewModels;
using OrgNet.Shared.DTOs;
using System;

namespace OrgNet.Desktop.Views;

public sealed partial class AnnouncementsPage : Page
{
    private readonly AnnouncementsViewModel _vm;

    public AnnouncementsPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<AnnouncementsViewModel>();
        Loaded += async (_, _) => await LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        LoadingBar.Visibility = Visibility.Visible;
        await _vm.LoadAsync();
        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private void UpdateUI()
    {
        ErrorBar.IsOpen = !string.IsNullOrEmpty(_vm.ErrorMessage);
        if (ErrorBar.IsOpen) ErrorBar.Message = _vm.ErrorMessage!;

        StatusBar.IsOpen = !string.IsNullOrEmpty(_vm.StatusMessage);
        if (StatusBar.IsOpen) StatusBar.Message = _vm.StatusMessage!;

        AnnouncementListView.ItemsSource = _vm.Announcements;
    }

    private async void OnNewAnnouncementClicked(object sender, RoutedEventArgs e)
    {
        var titleBox = new TextBox { PlaceholderText = "Announcement title" };
        var bodyBox = new TextBox { PlaceholderText = "Announcement body", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 120 };
        var pinnedCheck = new CheckBox { Content = "Pin this announcement", Margin = new Thickness(0, 8, 0, 0) };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(titleBox);
        panel.Children.Add(bodyBox);
        panel.Children.Add(pinnedCheck);

        var dialog = new ContentDialog
        {
            Title = "New Announcement",
            Content = panel,
            PrimaryButtonText = "Publish",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var title = titleBox.Text?.Trim();
        var body = bodyBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
        {
            ErrorBar.Message = "Title and body are required";
            ErrorBar.IsOpen = true;
            return;
        }

        LoadingBar.Visibility = Visibility.Visible;
        await _vm.CreateAsync((title, body, pinnedCheck.IsChecked == true));
        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private async void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid id) return;

        var confirm = new ContentDialog
        {
            Title = "Delete Announcement",
            Content = "Are you sure? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        LoadingBar.Visibility = Visibility.Visible;
        await _vm.DeleteAsync(id);
        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        LoadingBar.Visibility = Visibility.Visible;
        await _vm.LoadAsync();
        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }
}
