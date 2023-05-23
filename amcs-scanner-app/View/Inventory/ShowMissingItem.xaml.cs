using amcs_scanner_app.Client;
using amcs_scanner_app.Helper;
using amcs_scanner_app.Resources.Localization;
using amcs_shared_lib.Models.DTO;
using System.Collections.ObjectModel;
using Microsoft.Maui.Platform;

namespace amcs_scanner_app.View.Inventory;

public partial class ShowMissingItem : ContentPage
{
    private InventoryItemsPage _page;
    private ResultInventoryItem _resultItem;
    private ObservableCollection<ResultItemStoragePlace> _storagePlaces;
    private AppSettings _settings;
    //private ResultItemStoragePlace resultISP;
    private static App app => Application.Current as App;
    private readonly BarcodeParser _barcodeParser;
    public ShowMissingItem( AppSettings settings, InventoryItemsPage page)
	{
		InitializeComponent();
        _barcodeParser = new BarcodeParser(settings);
        _page = page;
        _settings = settings;
        SendItemBtn.HeightRequest = _settings.DisplayHeight * 0.08;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        app.ScannerScannedEvent += OnScannerScanned;
        ItemId.Text = "";
        StorageID.Text = "";
        Amount.Text = "";
        AmountUnit.Text = "";
        SendItemBtn.IsEnabled = true; 

    }


    private void CloseItem(object sender, EventArgs e)
    {
        CloseModal();
    }

    private void CloseModal()
    {
        app.ScannerScannedEvent -= OnScannerScanned;
#if ANDROID
        if (Platform.CurrentActivity.CurrentFocus != null)
            Platform.CurrentActivity.HideKeyboard(Platform.CurrentActivity.CurrentFocus);
#endif
        App.OrderItemsPageReload = false;
        Shell.Current.GoToAsync(nameof(InventoryItemsPage));
    }

    private void StorageAdd(object sender, TextChangedEventArgs e)
    {
        var temp = e.NewTextValue;
        if (temp.Length > 1 && temp[0] == '0')
        {
            Amount.Text = temp.Remove(0, 1);
            return;
        }

        var tempValue = e.OldTextValue;

        char[] delimiterChars = { ',', '.' };
        string[] dec = temp.Split(delimiterChars);
        decimal testAmount = TestDecimalPiece(dec[0]);

        if (!temp.Equals(dec[0]))
        {
            if (dec[0] == "") Amount.Text = "";
            else Amount.Text = tempValue;
            return;
        }

        if (testAmount < 0)
        {
            Amount.Text = tempValue;
            return;
        }

        Amount.Text = testAmount.ToString();

    }

    private decimal TestDecimalPiece(string value)
    {
        if (value == null || value == "") return 0;

        if (decimal.TryParse(value, out decimal number))
        {
            if (number >= 0)
                return number;
        }
        return -1;
    }

    private void SendPiece(object sender, EventArgs e)
    {
        //SendItemBtn.IsEnabled = false;
        SendPiece();
    }

    

    private void Entry_Completed(object sender, EventArgs e)
    {
        SendPiece();
    }

    private void SendPiece()
    {
        if(ItemId.Text == "" || StorageID.Text == "" || Amount.Text == "" || AmountUnit.Text == "" )
        {
            DisplayAlert("Fehlerhafte Eingabe", "","Ok");
            return;
        }

        new ItemStoragePlaceDTO();
        
        new ResultInventoryItem(new InventoryItemDTO(), 12);

        DisplayAlert("ItemId: " + ItemId.Text + ", StorageID: " + StorageID.Text, "Amount: " + Amount.Text + " " + AmountUnit.Text,"Ok");
        
        //_page.Send(_storagePlaces, _resultItem.Id);
        CloseModal();
    }

    private async void OnScannerScanned(object sender, ScannerEventArgs e)
    {
        var scan = e.Content;
        var codesScanned = new List<BarcodeContent>();
        try
        {
            codesScanned = _barcodeParser.ReadBarcode(scan);
            if (!codesScanned.Any())
                throw new InvalidOperationException(AppResources.NoBarcodeParsed);

            if (codesScanned.Count != 1)
                throw new InvalidOperationException(AppResources.MoreThanOneCodeScanned);

            var code = codesScanned[0];

            if (code.Identifier == _settings.ApplicationIdentifiers.Storage.Identifier) StorageID.Text = code.Value;
            else if (code.Identifier == _settings.ApplicationIdentifiers.Article.Identifier) ItemId.Text = code.Value;
            else throw new InvalidOperationException("Scan errors!");

        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
        }
    }



}