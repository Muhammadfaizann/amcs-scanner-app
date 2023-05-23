using amcs_scanner_app.View;
using amcs_scanner_app.View.Inventory;
using System;
using System.Globalization;

namespace amcs_scanner_app;

public partial class AppShell : Shell
{
    public AppShell(string lang = null)
    {
        if (!(lang is null))
        {
            CultureInfo culture = new CultureInfo(lang);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        InitializeComponent();

        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(OverviewPage), typeof(OverviewPage));
        Routing.RegisterRoute(nameof(PickingOrderPage), typeof(PickingOrderPage));
        Routing.RegisterRoute(nameof(PickingOrderItemsPage), typeof(PickingOrderItemsPage));
        Routing.RegisterRoute(nameof(ShowItemPiece), typeof(ShowItemPiece));
        Routing.RegisterRoute(nameof(ShowItemWiege), typeof(ShowItemWiege));
        Routing.RegisterRoute(nameof(FlyoutAbmelden), typeof(FlyoutAbmelden));
        Routing.RegisterRoute(nameof(FlyoutSettings), typeof(FlyoutSettings));
        Routing.RegisterRoute(nameof(FlyoutUser), typeof(FlyoutUser));
        Routing.RegisterRoute(nameof(WaitingForConnection), typeof(WaitingForConnection));
        Routing.RegisterRoute(nameof(InventoryOrdersPage), typeof(InventoryOrdersPage));
        Routing.RegisterRoute(nameof(InventoryItemsPage), typeof(InventoryItemsPage));
        Routing.RegisterRoute(nameof(ShowItem), typeof(ShowItem));
        Routing.RegisterRoute(nameof(ShowMissingItem), typeof(ShowMissingItem));
    }

    private void ToogleFlyout(object sender, EventArgs e)
    {
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Flyout;
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.Current.FlyoutIsPresented = false;
    }

    private void ToggleDarkMode(object sender, EventArgs e)
    {
        if (App.Current.UserAppTheme == AppTheme.Light)
        {
            App.Current.UserAppTheme = AppTheme.Dark;
        }
        else
        {
            App.Current.UserAppTheme = AppTheme.Light;
        }
    }

    private void UserBtn_Clicked(object sender, EventArgs e)
    {
        Shell.Current.GoToAsync(nameof(FlyoutUser));
        Shell.Current.FlyoutIsPresented = false;
    }

    private void SettingsBtn_Clicked(object sender, EventArgs e)
    {
        Shell.Current.GoToAsync(nameof(FlyoutSettings));
        Shell.Current.FlyoutIsPresented = false;
    }

    private void AboutBnt_Clicked(object sender, EventArgs e)
    {

    }

    private void AbmeldenBtn_Clicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new AppShell();
    }
}
