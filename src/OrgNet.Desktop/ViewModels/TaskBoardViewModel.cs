using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;

namespace OrgNet.Desktop.ViewModels;

public partial class TaskBoardViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;

    public TaskBoardViewModel(OrgNetApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private int _todoCount;
    [ObservableProperty] private int _inProgressCount;
    [ObservableProperty] private int _reviewCount;
    [ObservableProperty] private int _doneCount;

    public ObservableCollection<TaskItemDto> Tasks { get; } = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var tasks = await _api.GetTasksAsync();
            Tasks.Clear();
            if (tasks != null)
                foreach (var t in tasks) Tasks.Add(t);

            var summary = await _api.GetTaskSummaryAsync();
            if (summary != null)
            {
                TodoCount = summary.Todo;
                InProgressCount = summary.InProgress;
                ReviewCount = summary.Review;
                DoneCount = summary.Done;
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task CreateAsync((string Title, string? Description, TaskItemPriority Priority) args)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _api.CreateTaskAsync(new CreateTaskRequest(args.Title, args.Description, args.Priority));
            if (result != null) { StatusMessage = "Task created"; await LoadAsync(); }
            else ErrorMessage = _api.LastError ?? "Failed to create task";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task UpdateStatusAsync((Guid TaskId, TaskItemStatus Status) args)
    {
        ErrorMessage = null;
        try
        {
            var result = await _api.UpdateTaskAsync(new UpdateTaskRequest(args.TaskId, null, null, args.Status, null, null));
            if (result != null) await LoadAsync();
            else ErrorMessage = _api.LastError ?? "Failed to update task";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    public async Task DeleteAsync(Guid taskId)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            if (await _api.DeleteTaskAsync(taskId)) { StatusMessage = "Task deleted"; await LoadAsync(); }
            else ErrorMessage = _api.LastError ?? "Delete failed";
        }
        finally { IsLoading = false; }
    }
}
