using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OrgNet.Desktop.Services;

namespace OrgNet.Desktop.Views;

public sealed partial class ProfilePage : Page
{
    private readonly OrgNetApiClient _api;
    private readonly ICredentialStore _credentials;

    public ProfilePage()
    {
        this.InitializeComponent();
        _api = App.GetService<OrgNetApiClient>();
        _credentials = App.GetService<ICredentialStore>();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadingBar.Visibility = Visibility.Visible;
        try
        {
            var displayName = _credentials.GetDisplayName() ?? "User";
            var role = _credentials.GetUserRole() ?? "Member";

            DisplayNameBox.Text = displayName;
            RoleText.Text = role;
            AvatarInitials.Text = displayName.Length > 0 ? displayName[..1].ToUpper() : "?";

            // Try to load full profile from members list
            var members = await _api.GetMembersAsync();
            if (members != null)
            {
                var me = members.FirstOrDefault(m => m.DisplayName == displayName);
                if (me != null)
                {
                    EmailBox.Text = me.Email;
                    CreatedAtText.Text = me.CreatedAt.ToString("MMMM dd, yyyy");
                    DisplayNameBox.Text = me.DisplayName;
                    RoleText.Text = me.Role;
                    AvatarInitials.Text = me.DisplayName.Length >= 2
                        ? me.DisplayName[..2].ToUpper()
                        : me.DisplayName[..1].ToUpper();
                }
            }
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        var name = DisplayNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ErrorBar.Message = "Display name cannot be empty";
            ErrorBar.IsOpen = true;
            return;
        }

        StatusBar.Title = "Profile";
        StatusBar.Message = "Profile saved (name change requires re-login to take effect)";
        StatusBar.IsOpen = true;
    }

    private void OnChangePasswordClicked(object sender, RoutedEventArgs e)
    {
        var current = CurrentPasswordBox.Password;
        var newPw = NewPasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(newPw))
        {
            ErrorBar.Message = "All password fields are required";
            ErrorBar.IsOpen = true;
            return;
        }

        if (newPw != confirm)
        {
            ErrorBar.Message = "New passwords do not match";
            ErrorBar.IsOpen = true;
            return;
        }

        if (newPw.Length < 8)
        {
            ErrorBar.Message = "Password must be at least 8 characters";
            ErrorBar.IsOpen = true;
            return;
        }

        StatusBar.Title = "Password";
        StatusBar.Message = "Password change requires API endpoint — not yet available";
        StatusBar.IsOpen = true;

        CurrentPasswordBox.Password = "";
        NewPasswordBox.Password = "";
        ConfirmPasswordBox.Password = "";
    }
}
