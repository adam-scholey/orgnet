using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OrgNet.Desktop.ViewModels;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.Views;

public sealed partial class LoginPage : Page
{
    private readonly LoginViewModel _vm;
    private bool _isRegisterMode;
    private bool _isJoinMode;

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
                _vm.Sector = (SectorCombo.SelectedItem as Microsoft.UI.Xaml.Controls.ComboBoxItem)?.Tag?.ToString() ?? "Other";
                await _vm.RegisterCommand.ExecuteAsync(null);
            }
            else
            {
                await _vm.LoginCommand.ExecuteAsync(null);
            }

            ShowError();
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            ActionButton.IsEnabled = true;
        }
    }

    private void OnToggleMode(object sender, RoutedEventArgs e)
    {
        // If in join mode, go back to login first
        if (_isJoinMode)
        {
            SwitchToLoginRegister();
            return;
        }

        _isRegisterMode = !_isRegisterMode;

        OrgNameBox.Visibility = _isRegisterMode ? Visibility.Visible : Visibility.Collapsed;
        DisplayNameBox.Visibility = _isRegisterMode ? Visibility.Visible : Visibility.Collapsed;
        SectorCombo.Visibility = _isRegisterMode ? Visibility.Visible : Visibility.Collapsed;

        ActionButton.Content = _isRegisterMode ? "Create Organisation" : "Sign In";
        ToggleLink.Content = _isRegisterMode ? "Already have an account? Sign in" : "Create a new organisation";
    }

    private void OnJoinModeClicked(object sender, RoutedEventArgs e)
    {
        _isJoinMode = true;
        _isRegisterMode = false;
        ErrorBar.IsOpen = false;
        _vm.ResetJoinMode();

        LoginPanel.Visibility = Visibility.Collapsed;
        JoinPanel.Visibility = Visibility.Visible;
        JoinTokenStep.Visibility = Visibility.Visible;
        JoinAcceptStep.Visibility = Visibility.Collapsed;

        ToggleLink.Content = "Back to sign in";
        JoinLink.Visibility = Visibility.Collapsed;
    }

    private void SwitchToLoginRegister()
    {
        _isJoinMode = false;
        _isRegisterMode = false;
        ErrorBar.IsOpen = false;
        _vm.ResetJoinMode();

        LoginPanel.Visibility = Visibility.Visible;
        JoinPanel.Visibility = Visibility.Collapsed;

        ActionButton.Content = "Sign In";
        ToggleLink.Content = "Create a new organisation";
        JoinLink.Visibility = Visibility.Visible;

        OrgNameBox.Visibility = Visibility.Collapsed;
        DisplayNameBox.Visibility = Visibility.Collapsed;
        SectorCombo.Visibility = Visibility.Collapsed;
    }

    private async void OnLookupClicked(object sender, RoutedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        LoadingBar.Visibility = Visibility.Visible;
        LookupButton.IsEnabled = false;

        try
        {
            _vm.InviteToken = InviteTokenBox.Text;
            await _vm.LookupInviteCommand.ExecuteAsync(null);

            if (_vm.InviteLookedUp)
            {
                // Show the org preview and accept form
                JoinTokenStep.Visibility = Visibility.Collapsed;
                JoinAcceptStep.Visibility = Visibility.Visible;

                JoinOrgNameText.Text = _vm.InviteOrgName ?? "";
                JoinRoleText.Text = $"Role: {_vm.InviteRole}";
                JoinEmailText.Text = $"Email: {_vm.InviteEmail}";
                JoinInvitedByText.Text = $"Invited by {_vm.InvitedBy}";
            }

            ShowError();
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            LookupButton.IsEnabled = true;
        }
    }

    private async void OnJoinClicked(object sender, RoutedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        LoadingBar.Visibility = Visibility.Visible;
        JoinButton.IsEnabled = false;

        try
        {
            _vm.DisplayName = JoinDisplayNameBox.Text;
            _vm.Password = JoinPasswordBox.Password;
            await _vm.JoinCommand.ExecuteAsync(null);

            ShowError();
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            JoinButton.IsEnabled = true;
        }
    }

    private void ShowError()
    {
        if (!string.IsNullOrEmpty(_vm.ErrorMessage))
        {
            ErrorBar.Message = _vm.ErrorMessage;
            ErrorBar.IsOpen = true;
        }
    }
}
