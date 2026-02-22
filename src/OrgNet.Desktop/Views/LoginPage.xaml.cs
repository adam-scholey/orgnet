using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OrgNet.Desktop.ViewModels;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.Views;

public sealed partial class LoginPage : Page
{
    private readonly LoginViewModel _vm;
    private bool _isRegisterMode;

    public LoginPage()
    {
        this.InitializeComponent();
        _vm = App.GetService<LoginViewModel>();
    }

    private async void OnActionClicked(object sender, RoutedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        LoadingBar.Visibility = Visibility.Visible;
        ActionButton.IsEnabled = false;

        try
        {
            _vm.Email = EmailBox.Text;
            _vm.Password = PasswordBox.Password;

            if (_isRegisterMode)
            {
                _vm.OrganisationName = OrgNameBox.Text;
                _vm.DisplayName = DisplayNameBox.Text;
                await _vm.RegisterCommand.ExecuteAsync(null);
            }
            else
            {
                await _vm.LoginCommand.ExecuteAsync(null);
            }

            if (!string.IsNullOrEmpty(_vm.ErrorMessage))
            {
                ErrorBar.Message = _vm.ErrorMessage;
                ErrorBar.IsOpen = true;
            }
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            ActionButton.IsEnabled = true;
        }
    }

    private void OnToggleMode(object sender, RoutedEventArgs e)
    {
        _isRegisterMode = !_isRegisterMode;

        OrgNameBox.Visibility = _isRegisterMode ? Visibility.Visible : Visibility.Collapsed;
        DisplayNameBox.Visibility = _isRegisterMode ? Visibility.Visible : Visibility.Collapsed;

        ActionButton.Content = _isRegisterMode ? "Create Organisation" : "Sign In";
        ToggleLink.Content = _isRegisterMode ? "Already have an account? Sign in" : "Create a new organisation";
    }
}
