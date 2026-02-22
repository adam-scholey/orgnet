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

    public System.Collections.ObjectModel.ObservableCollection<ModuleInfoDto> Modules { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var modules = await _api.GetModulesAsync();
            Modules.Clear();
            if (modules != null)
                foreach (var m in modules) Modules.Add(m);
        }
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
