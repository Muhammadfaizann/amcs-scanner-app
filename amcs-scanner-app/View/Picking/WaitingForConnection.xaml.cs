using amcs_scanner_app.Handler;
using amcs_scanner_app.View.Inventory;

namespace amcs_scanner_app.View;

public partial class WaitingForConnection : ContentPage
{
    private readonly PickingHandler _pickingHandler;

    private readonly InventoryHandler _inventoryHandler;

    public WaitingForConnection(PickingHandler pickingHandler)
	{
		InitializeComponent();
		_pickingHandler = pickingHandler;
        WaitForCacheClearedPicking();
	}

    public WaitingForConnection(InventoryHandler inventoryHandler)
	{
		InitializeComponent();
		_inventoryHandler = inventoryHandler;
        WaitForCacheClearedInventory();
	}

    /// <summary>
    /// Diese Methode sorgt dafür, dass sich das Flyout öffnet
    /// Das FlyoutBehavior muss erst auf Disabled gesetzt werden, sonst funktioniert der erste klick nicht
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FlyoutOpen(object sender, EventArgs e)
    {
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Flyout;
        Shell.Current.FlyoutIsPresented = true;
    }

    /// <summary>
    /// Solange noch Orders im Cache sind wird hier gewartet
    /// </summary>
    public async void WaitForCacheClearedPicking()
	{
        SpinningStart();
        await Task.Delay(1000);
        while (_pickingHandler._orderCache.GetCachedMessages().Any())
		{
			await Task.Delay(100);
		}
        App.OrderPageReload = false;
        await Shell.Current.GoToAsync(nameof(PickingOrderPage));
        SpinningEnd();
    }

    public async void WaitForCacheClearedInventory()
	{
        SpinningStart();
        await Task.Delay(1000);
        while (_inventoryHandler._orderCache.GetCachedMessages().Any())
		{
			await Task.Delay(100);
		}
        App.OrderPageReload = false;
        await Shell.Current.GoToAsync(nameof(InventoryOrdersPage));
        SpinningEnd();
	}

    public ActivityIndicator SpinningStart()
    {
        Spinner.IsVisible = true;
        Spinner.IsRunning = true;

        return new ActivityIndicator { IsRunning = true, Color = Colors.Orange };
    }

    public ActivityIndicator SpinningEnd()
    {
        Spinner.IsVisible = false;
        Spinner.IsRunning = false;

        return new ActivityIndicator { IsRunning = false, Color = Colors.Orange };
    }
}