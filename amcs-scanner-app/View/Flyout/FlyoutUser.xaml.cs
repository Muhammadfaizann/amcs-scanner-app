using amcs_scanner_app.Resources.Localization;

namespace amcs_scanner_app.View;

public partial class FlyoutUser : ContentPage
{
    public string CurrentUser { get; set; }
    public FlyoutUser()
    {
        InitializeComponent();
        Switch();
    }

    private async void Switch()
    {
        CurrentUser = await SecureStorage.GetAsync("UserId");
        var pref = Preferences.Get("UserId", "");
        UserText.Text = AppResources.LoggedInAs + " " + CurrentUser;
        if (pref is "")
        {
            currentText.IsVisible = false;
            currentTextLogin.IsVisible = true;
            SpeichernBtn.IsVisible = true;
            DiscardBtn.IsVisible = false;
        }
        else
        {
            currentTextLogin.IsVisible = false;
            currentText.IsVisible = true;
            currentText.Text = AppResources.UserIsSaved;
            SpeichernBtn.IsVisible = false;
            DiscardBtn.IsVisible = true;
        }
    }

    private void FlyoutOpen(object sender, EventArgs e)
    {
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Flyout;
        Shell.Current.FlyoutIsPresented = true;
    }

    private void SpeichernClicked(object sender, EventArgs e)
    {
        Preferences.Default.Set("UserId", CurrentUser);
        Switch();
    }

    private void DiscardClicked(object sender, EventArgs e)
    {
        Preferences.Default.Set("UserId", "");
        Switch();
    }
}
