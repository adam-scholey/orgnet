using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;
using System;
using System.Collections.ObjectModel;

namespace OrgNet.Desktop.Views;

public sealed partial class UserManagementPage : Page
{
    private readonly OrgNetApiClient _api;
    private readonly ObservableCollection<UserProfileDto> _members = new();

    public UserManagementPage()
    {
        InitializeComponent();
        _api = App.Services.GetRequiredService<OrgNetApiClient>();
        MemberListView.ItemsSource = _members;
        Loaded += async (_, _) => await LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        LoadingBar.Visibility = Visibility.Visible;
        ErrorBar.IsOpen = false;

        try
        {
            var tenant = await _api.GetTenantAsync();
            if (tenant != null)
            {
                OrgNameText.Text = tenant.Name;
                OrgPlanText.Text = $"{tenant.Plan} plan · Created {tenant.CreatedAt:d}";
                MemberCountText.Text = tenant.MemberCount.ToString();
            }

            var members = await _api.GetMembersAsync();
            _members.Clear();
            if (members != null)
                foreach (var m in members) _members.Add(m);
        }
        catch (Exception ex)
        {
            ErrorBar.Message = ex.Message;
            ErrorBar.IsOpen = true;
        }

        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private async void OnInviteClicked(object sender, RoutedEventArgs e)
    {
        var emailBox = new TextBox { PlaceholderText = "user@example.com" };
        var roleCombo = new ComboBox { Header = "Role", SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        roleCombo.Items.Add("Member");
        roleCombo.Items.Add("Moderator");
        roleCombo.Items.Add("Admin");

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Enter the email address of the user to invite." });
        panel.Children.Add(emailBox);
        panel.Children.Add(roleCombo);

        var dialog = new ContentDialog
        {
            Title = "Invite User",
            Content = panel,
            PrimaryButtonText = "Send Invite",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var email = emailBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            ErrorBar.Message = "Valid email address required";
            ErrorBar.IsOpen = true;
            return;
        }

        var role = (roleCombo.SelectedItem as string) ?? "Member";

        LoadingBar.Visibility = Visibility.Visible;

        try
        {
            var result = await _api.PostInviteAsync(new InviteUserRequest(email, role));
            if (result != null && result.Success)
            {
                // Show the invite link so the admin can share it
                var linkBox = new TextBox
                {
                    Text = result.InviteLink,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                };
                var infoDialog = new ContentDialog
                {
                    Title = "Invitation Created",
                    Content = new StackPanel { Spacing = 8, Children =
                    {
                        new TextBlock { Text = $"Share this link with {email}. They can use it to join your organisation.", TextWrapping = TextWrapping.Wrap },
                        linkBox,
                        new TextBlock { Text = $"Expires: {result.ExpiresAt:g}", Opacity = 0.5, FontSize = 12 },
                    }},
                    CloseButtonText = "Done",
                    XamlRoot = this.XamlRoot
                };
                await infoDialog.ShowAsync();

                StatusBar.Message = $"Invitation sent to {email}";
                StatusBar.IsOpen = true;
                await LoadPageAsync();
            }
            else
            {
                ErrorBar.Message = _api.LastError ?? "Failed to send invitation";
                ErrorBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            ErrorBar.Message = ex.Message;
            ErrorBar.IsOpen = true;
        }

        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await LoadPageAsync();
    }
}
