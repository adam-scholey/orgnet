using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OrgNet.Desktop.Services;

namespace OrgNet.Desktop.Views;

public sealed partial class OnboardingPage : Page
{
    private int _currentStep = 1;

    public OnboardingPage()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var credentials = App.GetService<ICredentialStore>();
        OrgNameDisplay.Text = credentials.GetDisplayName() ?? "Your Organisation";
        OrgSectorDisplay.Text = $"Role: {credentials.GetUserRole() ?? "Member"}";
    }

    private void OnNextStep(object sender, RoutedEventArgs e)
    {
        if (_currentStep < 3) _currentStep++;
        UpdateStepVisibility();
    }

    private void OnPrevStep(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1) _currentStep--;
        UpdateStepVisibility();
    }

    private void UpdateStepVisibility()
    {
        Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;

        var primary = (SolidColorBrush)Application.Current.Resources["PrimaryBrush"];
        var subtle = (SolidColorBrush)Application.Current.Resources["SubtleBrush"];
        Step1Dot.Fill = _currentStep >= 1 ? primary : subtle;
        Step2Dot.Fill = _currentStep >= 2 ? primary : subtle;
        Step3Dot.Fill = _currentStep >= 3 ? primary : subtle;
    }

    private void OnFinishOnboarding(object sender, RoutedEventArgs e)
    {
        // Navigate to shell/dashboard
        if (App.MainWindow?.Content is Frame rootFrame)
        {
            rootFrame.Navigate(typeof(ShellPage));
        }
    }
}
