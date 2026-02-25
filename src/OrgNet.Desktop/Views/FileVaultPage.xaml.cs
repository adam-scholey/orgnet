using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using OrgNet.Desktop.Services;
using OrgNet.Desktop.ViewModels;
using OrgNet.Shared.DTOs;
using Windows.Storage.Pickers;
using Windows.Storage;
using System;

namespace OrgNet.Desktop.Views;

public sealed partial class FileVaultPage : Page
{
    private readonly FileVaultViewModel _vm;

    public FileVaultPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<FileVaultViewModel>();
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

        FileListView.ItemsSource = _vm.Files;
        FileCountText.Text = $"{_vm.Files.Count} files";
        EmptyText.Visibility = _vm.Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnUploadClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            // Prompt for encryption PIN and shared toggle
            var pinBox = new PasswordBox { PlaceholderText = "Enter encryption PIN (min 4 chars)" };
            var sharedCheck = new CheckBox { Content = "Share with organisation", Margin = new Thickness(0, 8, 0, 0) };
            var panel = new StackPanel();
            panel.Children.Add(pinBox);
            panel.Children.Add(sharedCheck);

            var dialog = new ContentDialog
            {
                Title = "Encrypt & Upload",
                Content = panel,
                PrimaryButtonText = "Upload",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var pin = pinBox.Password;
            if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
            {
                ErrorBar.Message = "PIN must be at least 4 characters";
                ErrorBar.IsOpen = true;
                return;
            }

            LoadingBar.Visibility = Visibility.Visible;

            var buffer = await FileIO.ReadBufferAsync(file);
            var bytes = new byte[buffer.Length];
            using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
                reader.ReadBytes(bytes);

            var base64 = Convert.ToBase64String(bytes);
            var isShared = sharedCheck.IsChecked == true;

            await _vm.UploadAsync((file.Name, base64, pin, isShared));
            UpdateUI();
            LoadingBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ErrorBar.Message = $"Upload error: {ex.Message}";
            ErrorBar.IsOpen = true;
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnDownloadClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid fileId) return;

        try
        {
            var pinDialog = new ContentDialog
            {
                Title = "Decryption PIN",
                Content = new PasswordBox { PlaceholderText = "Enter decryption PIN" },
                PrimaryButtonText = "Download",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            if (await pinDialog.ShowAsync() != ContentDialogResult.Primary) return;
            var pin = ((PasswordBox)pinDialog.Content).Password;
            if (string.IsNullOrWhiteSpace(pin)) return;

            LoadingBar.Visibility = Visibility.Visible;

            var api = App.Services.GetRequiredService<OrgNetApiClient>();
            var data = await api.DownloadFileAsync(fileId, pin);

            if (data != null)
            {
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("All Files", new[] { "." });

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                var saveFile = await savePicker.PickSaveFileAsync();
                if (saveFile != null)
                {
                    await FileIO.WriteBytesAsync(saveFile, data);
                    StatusBar.Message = $"Downloaded to {saveFile.Path}";
                    StatusBar.IsOpen = true;
                }
            }
            else
            {
                ErrorBar.Message = api.LastError ?? "Download failed";
                ErrorBar.IsOpen = true;
            }

            LoadingBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ErrorBar.Message = $"Download error: {ex.Message}";
            ErrorBar.IsOpen = true;
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid fileId) return;

        var confirm = new ContentDialog
        {
            Title = "Delete File",
            Content = "Are you sure? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        LoadingBar.Visibility = Visibility.Visible;
        await _vm.DeleteAsync(fileId);
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

    private void OnClosePreview(object sender, RoutedEventArgs e)
    {
        PreviewPanel.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
    }
}
