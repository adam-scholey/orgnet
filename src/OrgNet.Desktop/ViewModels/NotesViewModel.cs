using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class NotesViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;

    public NotesViewModel(OrgNetApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private CollabNoteDto? _selectedNote;

    public ObservableCollection<CollabNoteDto> Notes { get; } = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var notes = await _api.GetNotesAsync();
            Notes.Clear();
            if (notes != null)
                foreach (var n in notes) Notes.Add(n);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task CreateAsync((string Title, string Content) args)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _api.CreateNoteAsync(new CreateNoteRequest(args.Title, args.Content));
            if (result != null) { StatusMessage = "Note created"; await LoadAsync(); }
            else ErrorMessage = _api.LastError ?? "Failed to create note";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task UpdateAsync((Guid NoteId, string Title, string Content, bool IsPinned) args)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _api.UpdateNoteAsync(new UpdateNoteRequest(args.NoteId, args.Title, args.Content, args.IsPinned));
            if (result != null) { StatusMessage = "Note updated"; await LoadAsync(); }
            else ErrorMessage = _api.LastError ?? "Failed to update note";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task DeleteAsync(Guid noteId)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            if (await _api.DeleteNoteAsync(noteId)) { StatusMessage = "Note deleted"; await LoadAsync(); }
            else ErrorMessage = _api.LastError ?? "Delete failed";
        }
        finally { IsLoading = false; }
    }
}
