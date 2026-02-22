using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class FileVaultViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;

    public FileVaultViewModel(OrgNetApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;

    public ObservableCollection<OrgFileDto> Files { get; } = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var files = await _api.GetFilesAsync();
            Files.Clear();
            if (files != null)
                foreach (var f in files) Files.Add(f);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task UploadAsync((string FileName, string Base64Content, string Pin, bool IsShared) args)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _api.UploadFileAsync(new UploadFileRequest(args.FileName, args.Base64Content, args.Pin, args.IsShared));
            if (result != null) { StatusMessage = $"Uploaded {args.FileName}"; await LoadAsync(); }
            else ErrorMessage = _api.LastError ?? "Upload failed";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task DeleteAsync(Guid fileId)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            if (await _api.DeleteFileAsync(fileId)) { StatusMessage = "File deleted"; await LoadAsync(); }
            else ErrorMessage = _api.LastError ?? "Delete failed";
        }
        finally { IsLoading = false; }
    }
}
