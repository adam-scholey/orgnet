using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using OrgNet.Desktop.Services;
using OrgNet.Desktop.ViewModels;
using Windows.Storage.Pickers;
using Windows.Storage;
using System;
using System.IO;

namespace OrgNet.Desktop.Views;

public sealed partial class FileFlowPage : Page
{
    private readonly FileFlowViewModel _vm;

    public FileFlowPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<FileFlowViewModel>();
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
        // Error
        if (!string.IsNullOrEmpty(_vm.ErrorMessage))
        {
            ErrorBar.Message = _vm.ErrorMessage;
            ErrorBar.IsOpen = true;
        }
        else
        {
            ErrorBar.IsOpen = false;
        }

        // Status
        if (!string.IsNullOrEmpty(_vm.StatusMessage))
        {
            StatusBar.Message = _vm.StatusMessage;
            StatusBar.IsOpen = true;
        }
        else
        {
            StatusBar.IsOpen = false;
        }

        // Connection state
        if (_vm.IsEnabled && !_vm.IsConnected)
        {
            NotConnectedBar.IsOpen = true;
            ConnectPanel.Visibility = Visibility.Visible;
            ToolbarPanel.Visibility = Visibility.Collapsed;
        }
        else if (_vm.IsConnected)
        {
            NotConnectedBar.IsOpen = false;
            ConnectPanel.Visibility = Visibility.Collapsed;
            ToolbarPanel.Visibility = Visibility.Visible;
            FileCountText.Text = $"{_vm.TotalFiles} files · {_vm.TotalSizeDisplay}";
        }
        else
        {
            NotConnectedBar.IsOpen = false;
            ConnectPanel.Visibility = Visibility.Collapsed;
            ToolbarPanel.Visibility = Visibility.Collapsed;
        }

        // File list
        FileListView.ItemsSource = _vm.Files;
    }

    private async void OnConnectClicked(object sender, RoutedEventArgs e)
    {
        var password = ConnectPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorBar.Message = "Password is required";
            ErrorBar.IsOpen = true;
            return;
        }

        ConnectButton.IsEnabled = false;
        LoadingBar.Visibility = Visibility.Visible;

        await _vm.ConnectAsync(password);
        UpdateUI();

        LoadingBar.Visibility = Visibility.Collapsed;
        ConnectButton.IsEnabled = true;
    }

    private async void OnUploadClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            // WinUI 3 requires window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            // Prompt for encryption pin
            var pinDialog = new ContentDialog
            {
                Title = "Encryption PIN",
                Content = new PasswordBox { Name = "PinBox", PlaceholderText = "Enter encryption PIN" },
                PrimaryButtonText = "Upload",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var dialogResult = await pinDialog.ShowAsync();
            if (dialogResult != ContentDialogResult.Primary) return;

            var pinBox = (PasswordBox)pinDialog.Content;
            var pin = pinBox.Password;
            if (string.IsNullOrWhiteSpace(pin))
            {
                ErrorBar.Message = "Encryption PIN is required";
                ErrorBar.IsOpen = true;
                return;
            }

            LoadingBar.Visibility = Visibility.Visible;

            var buffer = await FileIO.ReadBufferAsync(file);
            var bytes = new byte[buffer.Length];
            using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
            {
                reader.ReadBytes(bytes);
            }
            var base64 = Convert.ToBase64String(bytes);

            await _vm.UploadFileAsync((file.Name, base64, pin));
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
        if (sender is Button btn && btn.Tag is int fileId)
        {
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

                var dialogResult = await pinDialog.ShowAsync();
                if (dialogResult != ContentDialogResult.Primary) return;

                var pin = ((PasswordBox)pinDialog.Content).Password;
                if (string.IsNullOrWhiteSpace(pin)) return;

                LoadingBar.Visibility = Visibility.Visible;

                var data = await App.Services.GetRequiredService<OrgNetApiClient>()
                    .DownloadFileFlowFileAsync(fileId, pin);

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
                    ErrorBar.Message = App.Services.GetRequiredService<OrgNetApiClient>().LastError ?? "Download failed";
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
    }

    private async void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int fileId)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Delete File",
                Content = "Are you sure you want to delete this file? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            LoadingBar.Visibility = Visibility.Visible;
            await _vm.DeleteFileAsync(fileId);
            UpdateUI();
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        LoadingBar.Visibility = Visibility.Visible;
        await _vm.LoadAsync();
        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }
}
