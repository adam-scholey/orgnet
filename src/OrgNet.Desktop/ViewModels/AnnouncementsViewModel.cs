using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class AnnouncementsViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;

    public AnnouncementsViewModel(OrgNetApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;

    public ObservableCollection<AnnouncementDto> Announcements { get; } = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _api.GetAnnouncementsAsync();
            Announcements.Clear();
            if (items != null)
                foreach (var a in items) Announcements.Add(a);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task CreateAsync((string Title, string Body, bool IsPinned) args)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _api.CreateAnnouncementAsync(new CreateAnnouncementRequest(args.Title, args.Body, args.IsPinned));
            if (result != null) { StatusMessage = "Announcement published"; await LoadAsync(); }
            else ErrorMessage = _api.LastError ?? "Failed to publish announcement";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task DeleteAsync(Guid id)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            if (await _api.DeleteAnnouncementAsync(id)) { StatusMessage = "Announcement deleted"; await LoadAsync(); }
            else ErrorMessage = _api.LastError ?? "Delete failed";
        }
        finally { IsLoading = false; }
    }
}
