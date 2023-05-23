using amcs_scanner_app.Client;
using amcs_scanner_app.Helper;
using amcs_scanner_app.Resources.Localization;
using System.Globalization;
using Microsoft.Maui.Platform;

namespace amcs_scanner_app.View;

public partial class LoginPage : ContentPage
{
    private readonly UserServiceClient _userClient;
    private readonly ConfigurationClient _configClient;
    private readonly AppSettings _settings;
    private readonly BarcodeParser _barcodeParser;
    private static App App => Application.Current as App;
    private double layoutHeight = 0;
    public LoginPage(AppSettings settings, UserServiceClient userClient, ConfigurationClient configClient)
    {
        InitializeComponent();

       
        VersionNumberlbl.Text = AppInfo.Current.VersionString;

        _configClient = configClient;
        _settings = settings;
        _barcodeParser = new BarcodeParser(settings);
        _userClient = userClient;
        App.ScannerScannedEvent += OnScannerScanned;

        LoginBtn.Clicked += LoginBtnClicked;
        Reconnect.Clicked += ReconnectClicked;

        ErrorMessage.IsVisible = false;
        var savedUser = Preferences.Default.Get("UserId", "");
        if (!(savedUser == ""))
        {
            SkipLogin(savedUser);
        }

        Setup();
    }

    private async void Setup()
    {
        if (!_configClient.Connect() || !_userClient.Connect())
        {
            ErrorMessage.IsVisible = true;
            ErrorMessage.Text = "No Connection..";
            LoginBtn.IsVisible = false;
            Reconnect.IsVisible = true;
        }
        else
        {
            ErrorMessage.IsVisible = false;
            LoginBtn.IsVisible = true;
            Reconnect.IsVisible = false;
            await _configClient.LoadSettings();
        }

        if (DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape)
        {
            TitleLbl.IsVisible = false;
            TextLbl.IsVisible = false;
            OrScanLbl.IsVisible = false;
            VersionNumberlbl.IsVisible = false;
        }
    }

    public void Reload()
    {
        var cultureInfo = new CultureInfo(LanguagePicker.Items[LanguagePicker.SelectedIndex]);
        App.ChangeLanguage(LanguagePicker.Items[LanguagePicker.SelectedIndex]);
    }

    private void UpdateLanguage(object sender, EventArgs e) => this.Reload();

    private async void SkipLogin(string userId) => await AuthenticateUser(userId);

    private async void OnScannerScanned(object sender, ScannerEventArgs e)
    {
        var scan = e.Content;
        var codesScanned = new List<BarcodeContent>();
        try
        {
            codesScanned = _barcodeParser.ReadBarcode(scan);
            if (!codesScanned.Any())
                throw new InvalidOperationException(AppResources.NoBarcodeParsed);

            if (codesScanned.Count() == 1)
            {
                var code = codesScanned[0];
                if (!(code.Identifier == _settings.ApplicationIdentifiers.User.Identifier))
                    throw new InvalidOperationException(AppResources.ScannedCodeWasNoUser);

                await AuthenticateUser(code.Value);
            }
            else
            {
                throw new Exception(AppResources.MoreThanOneCodeScanned);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage.IsVisible = true;
            ErrorMessage.Text = ex.Message;
        }
    }

    public async void LoginBtnClicked(object sender, EventArgs e)
    {
        try
        {
            var id = UserToken.Text.Trim();

            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException(AppResources.NoUserGiven);

            await AuthenticateUser(id);
        }
        catch (Exception ex)
        {
            ErrorMessage.IsVisible = true;
            ErrorMessage.Text = ex.Message;
        }
    }

    public void ReconnectClicked(object sender, EventArgs e) => Setup();

    public async Task AuthenticateUser(string Id)
    {
        try
        {
            if (await _userClient.Authenticate(Id))
            {
#if ANDROID               
                if(Platform.CurrentActivity.CurrentFocus != null)
                Platform.CurrentActivity.HideKeyboard(Platform.CurrentActivity.CurrentFocus);
#endif
                App.ScannerScannedEvent -= OnScannerScanned;
                await Shell.Current.GoToAsync(nameof(OverviewPage));
            }
            else
            {
                throw new UnauthorizedAccessException(AppResources.FailedToAuthenticate);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage.IsVisible = true;
            ErrorMessage.Text = ex.Message;
            return;
        }
    }

    private async void UserTokenCompleted(object sender, EventArgs e)
    {
        try
        {
            var id = UserToken.Text.Trim();

            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException(AppResources.NoUserGiven);
            await AuthenticateUser(id);
        }
        catch (Exception ex)
        {
            ErrorMessage.IsVisible = true;
            ErrorMessage.Text = ex.Message;
        }
    }

    private void CheckBox_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {

    }
    private void LayoutSizeChanged(object sender, EventArgs e)
    {
        if (layoutHeight < LoginFlexLayout.Height)
        {
            TitleLbl.IsVisible = true;
            TextLbl.IsVisible = true;
            OrScanLbl.IsVisible = true;
            ScannenImg.IsVisible = true;
            VersionNumberlbl.IsVisible = true;
        }
        else
        {
            TitleLbl.IsVisible = false;
            TextLbl.IsVisible = false;
            OrScanLbl.IsVisible = false;
            VersionNumberlbl.IsVisible = false;
        }
        layoutHeight = LoginFlexLayout.Height;
    }

}

