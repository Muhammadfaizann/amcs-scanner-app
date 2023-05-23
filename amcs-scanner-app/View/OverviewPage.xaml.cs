using amcs_scanner_app.Client;
using amcs_scanner_app.View.Inventory;

namespace amcs_scanner_app.View;

public partial class OverviewPage : ContentPage
{
    private readonly UserServiceClient _userClient;
    private AppSettings _settings;
    public OverviewPage(UserServiceClient userClient, AppSettings settings)
    {
        _userClient = userClient;
        _settings = settings;
        InitializeComponent();
        Setup();
    }

    public async void Setup()
    {
        //check if logged in
        if (!await _userClient.IsAuthenticated())
        {
            await Shell.Current.GoToAsync(nameof(LoginPage));
        }
        //CheckPermissions();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CheckPermissions();
    }


    private void CheckPermissions()
    {

        var testAdmin = SecureStorage.GetAsync("IsAdmin").Result;
        var testControlePicking = SecureStorage.GetAsync("IsControlePickings").Result;
        var testManagePicking = SecureStorage.GetAsync("IsManagePickings").Result;
        var testPerformPicking = SecureStorage.GetAsync("IsPerformPickings").Result;
        var testPerformInventory = SecureStorage.GetAsync("IsPerformInventory").Result;

        //WareEntryBtn
        if (false) IsButtonEnabled(WareEntryBtn, true);
        else IsButtonEnabled(WareEntryBtn, false);

        //LagerhaltungBtn
        if (false) IsButtonEnabled(LagerhaltungBtn, true);
        else IsButtonEnabled(LagerhaltungBtn, false);

        //PickingBtn
        if (
               (testAdmin != null && testAdmin.Equals("true"))
            || (testControlePicking != null && testControlePicking.Equals("true"))
            || (testManagePicking != null && testManagePicking.Equals("true"))
            || (testPerformPicking != null && testPerformPicking.Equals("true"))
           )
            IsButtonEnabled(PickingBtn, true);

        else IsButtonEnabled(PickingBtn, false);

        //InventurBtn
        if (
               (testAdmin != null && testAdmin.Equals("true"))
            || (testPerformInventory != null && testPerformInventory.Equals("true"))
           )
            IsButtonEnabled(InventurBtn, true);
        else IsButtonEnabled(InventurBtn, false);

    }

    private void IsButtonEnabled(Button button, bool isEnabled)
    {
        button.IsEnabled = isEnabled;
        if (isEnabled) button.Opacity = 1;
        else button.Opacity = 0.5;
    }

    private async void NavigateToPicking(object sender, EventArgs e)
    {
        await NavigateToPickings();
    }

    private Task NavigateToPickings()
    {
        _settings.DisplayHeight = base.Height;
        _settings.DisplayWidth = base.Width;
        //DisplayAlert("DisplayHeight: " + _settings.DisplayHeight, "DisplayWidth" + _settings.DisplayWidth, "Ok");
        App.OrderPageReload = true;
        return Shell.Current.GoToAsync(nameof(PickingOrderPage));
    }

    private void NavigateToWareEntry(object sender, EventArgs e)
    {

    }

    private void NavigateToStorage(object sender, EventArgs e)
    {

    }

    private async void NavigateToInventur(object sender, EventArgs e)
    {
        await NavigateToInventur();
    }

    private Task NavigateToInventur()
    {
        _settings.DisplayHeight = base.Height;
        _settings.DisplayWidth = base.Width;
        App.OrderPageReload = true;
        return Shell.Current.GoToAsync(nameof(InventoryOrdersPage));
    }

    /// <summary>
    /// This method causes the flyout to open
    /// The FlyoutBehavior must first be set to Disabled, otherwise the first click will not work
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FlyoutOpen(object sender, EventArgs e)
    {
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Flyout;
        Shell.Current.FlyoutIsPresented = true;
    }
}
