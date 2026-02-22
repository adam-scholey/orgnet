using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class ModulesViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;

    public ModulesViewModel(OrgNetApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<ModuleInfoDto> Modules { get; } = new();

    // Built-in modules that ship with OrgNet
    private static readonly List<ModuleInfoDto> BuiltInModules =
    [
        new("OrgNet.Chat", "Team Messaging", "Real-time chat with channels, powered by SignalR. Send messages, create channels, and collaborate instantly.", "1.0.0", "Enabled", null),
        new("OrgNet.FileStorage", "Secure File Storage", "AES-256 encrypted file vault. Upload, download, and share files securely within your organisation.", "1.0.0", "Enabled", null),
        new("OrgNet.Notes", "Collaboration Notes", "Shared documents and notes with real-time edit tracking. Pin important notes for the team.", "1.0.0", "Enabled", null),
        new("OrgNet.TaskBoard", "Task Board", "Kanban-style task management with priorities, assignees, and status tracking (Todo, In Progress, Review, Done).", "1.0.0", "Enabled", null),
        new("OrgNet.Announcements", "Announcements", "Organisation-wide broadcasts with pinning and expiry. Real-time delivery via SignalR.", "1.0.0", "Enabled", null),
        new("OrgNet.UserManagement", "User Management", "View members, invite new users, and manage roles across your organisation.", "1.0.0", "Enabled", null),
        new("OrgNet.Devices", "Device Management", "Track and manage devices connected to your organisation. Trust levels and revocation.", "1.0.0", "Enabled", null),
        new("OrgNet.Audit", "Audit Logging", "Comprehensive audit trail of all actions within your organisation for compliance and security.", "1.0.0", "Enabled", null),
    ];

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var modules = await _api.GetModulesAsync();
            Modules.Clear();

            if (modules != null && modules.Count > 0)
            {
                foreach (var m in modules) Modules.Add(m);
            }
            else
            {
                // Show built-in modules when plugin loader returns empty
                foreach (var m in BuiltInModules) Modules.Add(m);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ToggleModuleAsync(ModuleInfoDto module)
    {
        var enable = module.Status != "Enabled";
        await _api.ToggleModuleAsync(module.ModuleId, enable);
        await LoadAsync();
    }
}
