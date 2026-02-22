using Microsoft.UI.Xaml.Controls;

namespace OrgNet.Desktop.Services;

/// <summary>
/// Centralised navigation service wrapping the root Frame.
/// ViewModels use this to navigate without coupling to UI framework types.
/// </summary>
public class NavigationService : INavigationService
{
    private Frame? _frame;

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public void SetFrame(Frame frame) => _frame = frame;

    public void NavigateTo(Type pageType, object? parameter = null)
    {
        _frame?.Navigate(pageType, parameter);
    }

    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
            _frame.GoBack();
    }
}
