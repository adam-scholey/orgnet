using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using OrgNet.Desktop.ViewModels;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;
using System;

namespace OrgNet.Desktop.Views;

public sealed partial class TaskBoardPage : Page
{
    private readonly TaskBoardViewModel _vm;

    public TaskBoardPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<TaskBoardViewModel>();
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

        TaskListView.ItemsSource = _vm.Tasks;
        TodoCountText.Text = _vm.TodoCount.ToString();
        InProgressCountText.Text = _vm.InProgressCount.ToString();
        ReviewCountText.Text = _vm.ReviewCount.ToString();
        DoneCountText.Text = _vm.DoneCount.ToString();
    }

    private async void OnNewTaskClicked(object sender, RoutedEventArgs e)
    {
        var titleBox = new TextBox { PlaceholderText = "Task title" };
        var descBox = new TextBox { PlaceholderText = "Description (optional)", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80 };
        var priorityCombo = new ComboBox { Header = "Priority", SelectedIndex = 1, HorizontalAlignment = HorizontalAlignment.Stretch };
        priorityCombo.Items.Add("Low");
        priorityCombo.Items.Add("Medium");
        priorityCombo.Items.Add("High");
        priorityCombo.Items.Add("Critical");

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(titleBox);
        panel.Children.Add(descBox);
        panel.Children.Add(priorityCombo);

        var dialog = new ContentDialog
        {
            Title = "New Task",
            Content = panel,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var title = titleBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ErrorBar.Message = "Title is required";
            ErrorBar.IsOpen = true;
            return;
        }

        var priority = (TaskItemPriority)(priorityCombo.SelectedIndex);

        LoadingBar.Visibility = Visibility.Visible;
        await _vm.CreateAsync((title, descBox.Text?.Trim(), priority));
        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private async void OnStatusChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.Tag is Guid taskId && combo.SelectedItem is ComboBoxItem item)
        {
            var statusText = item.Content?.ToString();
            if (Enum.TryParse<TaskItemStatus>(statusText, out var status))
            {
                await _vm.UpdateStatusAsync((taskId, status));
                UpdateUI();
            }
        }
    }

    private async void OnDeleteTaskClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid taskId) return;

        var confirm = new ContentDialog
        {
            Title = "Delete Task",
            Content = "Are you sure? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        LoadingBar.Visibility = Visibility.Visible;
        await _vm.DeleteAsync(taskId);
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
