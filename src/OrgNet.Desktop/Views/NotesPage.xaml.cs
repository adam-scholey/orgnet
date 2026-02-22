using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using OrgNet.Desktop.ViewModels;
using OrgNet.Shared.DTOs;
using System;

namespace OrgNet.Desktop.Views;

public sealed partial class NotesPage : Page
{
    private readonly NotesViewModel _vm;
    private Guid? _currentNoteId;

    public NotesPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<NotesViewModel>();
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

        NoteListView.ItemsSource = _vm.Notes;
    }

    private void OnNoteSelected(object sender, SelectionChangedEventArgs e)
    {
        if (NoteListView.SelectedItem is CollabNoteDto note)
        {
            _currentNoteId = note.Id;
            TitleBox.Text = note.Title;
            ContentBox.Text = note.Content;
            PinnedCheck.IsChecked = note.IsPinned;
            EditorInfo.Text = note.LastEditedBy != null
                ? $"Last edited by {note.LastEditedBy} · {note.LastEditedAt:g}"
                : $"Created by {note.CreatedBy} · {note.CreatedAt:g}";
            DeleteButton.Visibility = Visibility.Visible;
        }
    }

    private void OnNewNoteClicked(object sender, RoutedEventArgs e)
    {
        _currentNoteId = null;
        TitleBox.Text = string.Empty;
        ContentBox.Text = string.Empty;
        PinnedCheck.IsChecked = false;
        EditorInfo.Text = "New note";
        DeleteButton.Visibility = Visibility.Collapsed;
        NoteListView.SelectedItem = null;
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ErrorBar.Message = "Title is required";
            ErrorBar.IsOpen = true;
            return;
        }

        LoadingBar.Visibility = Visibility.Visible;

        if (_currentNoteId.HasValue)
        {
            await _vm.UpdateAsync((_currentNoteId.Value, title, ContentBox.Text ?? "", PinnedCheck.IsChecked == true));
        }
        else
        {
            await _vm.CreateAsync((title, ContentBox.Text ?? ""));
        }

        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private async void OnDeleteNoteClicked(object sender, RoutedEventArgs e)
    {
        if (!_currentNoteId.HasValue) return;

        var confirm = new ContentDialog
        {
            Title = "Delete Note",
            Content = "Are you sure? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        LoadingBar.Visibility = Visibility.Visible;
        await _vm.DeleteAsync(_currentNoteId.Value);
        _currentNoteId = null;
        TitleBox.Text = string.Empty;
        ContentBox.Text = string.Empty;
        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }
}
