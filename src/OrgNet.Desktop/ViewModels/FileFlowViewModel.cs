using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class FileFlowViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;
    private readonly ICredentialStore _credentials;

    public FileFlowViewModel(OrgNetApiClient api, ICredentialStore credentials)
    {
        _api = api;
        _credentials = credentials;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private int _totalFiles;
    [ObservableProperty] private string _totalSizeDisplay = "0 B";

    public ObservableCollection<FileFlowFileDto> Files { get; } = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var status = await _api.GetFileFlowStatusAsync();
            if (status == null)
            {
                ErrorMessage = _api.LastError ?? "Cannot reach OrgNet API";
                IsEnabled = false;
                return;
            }

            IsEnabled = status.Enabled;
            IsConnected = status.Connected;

            if (!status.Enabled)
            {
                StatusMessage = "FileFlow integration is disabled";
                return;
            }

            if (!status.Connected)
            {
                StatusMessage = "Not connected to FileFlow — click Connect to authenticate";
                return;
            }

            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ConnectAsync(string password)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Use the stored email from credentials (same as OrgNet login)
            var success = await _api.ConnectFileFlowAsync("", password);
            if (success)
            {
                IsConnected = true;
                StatusMessage = null;
                await RefreshFilesAsync();
            }
            else
            {
                ErrorMessage = _api.LastError ?? "Failed to connect to FileFlow";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task UploadFileAsync((string FileName, string Base64Content, string Pin) args)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _api.UploadFileFlowFileAsync(args.FileName, args.Base64Content, args.Pin);
            if (result != null)
            {
                StatusMessage = $"Uploaded {args.FileName}";
                await RefreshFilesAsync();
            }
            else
            {
                ErrorMessage = _api.LastError ?? "Upload failed";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DeleteFileAsync(int fileId)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var success = await _api.DeleteFileFlowFileAsync(fileId);
            if (success)
            {
                StatusMessage = "File deleted";
                await RefreshFilesAsync();
            }
            else
            {
                ErrorMessage = _api.LastError ?? "Delete failed";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshFilesAsync()
    {
        var fileList = await _api.GetFileFlowFilesAsync();
        Files.Clear();

        if (fileList?.Files != null)
        {
            foreach (var f in fileList.Files)
                Files.Add(f);

            TotalFiles = fileList.TotalCount;
            TotalSizeDisplay = FormatBytes(fileList.TotalSize);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
