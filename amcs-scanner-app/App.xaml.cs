using amcs_scanner_app.View;
using amcs_scanner_app.View.Inventory;
using System.Globalization;

namespace amcs_scanner_app;
public partial class App : Application
{
    public EventHandler<ScannerEventArgs> ScannerScannedEvent;
    public string ScannedValue { get; set; } = string.Empty;
    public static bool OrderPageReload { get; set; } = true;
    public static bool OrderItemsPageReload { get; set; } = true;
    public static List<ResultOrder> Orders { get; set; } = new List<ResultOrder>();
    public static List<ResultInventoryOrder> InventoryOrders { get; set; } = new List<ResultInventoryOrder>();
    public static ResultItem ResultItem;
    public static ResultInventoryItem ResultInventoryItem;
    public bool IsActive { get; set; }
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
        App.Current.UserAppTheme = AppTheme.Light;
    }

    public static void ChangeLanguage(string language)
    {
        CultureInfo culture = new CultureInfo(language);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        App.Current.MainPage = new AppShell();
    }

    /// <summary>
    /// In Dauerschleife das Clipboard auslesen und Eventhandler auslösen
    /// </summary>
    protected async override void OnStart()
    {
        base.OnStart();

        await Clipboard.SetTextAsync(string.Empty);

        IsActive = true;
        while (true)
        {
            await Task.Delay(100);

            if (!IsActive)
                continue;

            var clipboardString = await Clipboard.GetTextAsync();
            var content = (clipboardString is null) ? string.Empty : clipboardString;

            if (!string.IsNullOrEmpty(content))
            {
                OnScannerScanned(content);
                await Clipboard.SetTextAsync(string.Empty).ConfigureAwait(false);
            }
        }
    }

    private void OnScannerScanned(string scannedValue)
    {
        ScannerScannedEvent?.Invoke(this, new ScannerEventArgs(scannedValue));
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        IsActive = false;
    }

    protected override void OnResume()
    {
        base.OnResume();
        IsActive = true;
    }
    public static Color GetColorResource(string ColorName)
    {
        App.Current.Resources.TryGetValue(ColorName, out var colorResource);
        return (Color)colorResource;
    }
}

public class ScannerEventArgs
{
    public string Content { get; set; }

    public ScannerEventArgs(string content)
    {
        Content = content;
    }
}