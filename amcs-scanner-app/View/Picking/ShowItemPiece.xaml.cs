using amcs_scanner_app.Client;
using amcs_scanner_app.Helper;
using amcs_scanner_app.Resources.Localization;
using Microsoft.Maui.Platform;
using System.Collections.ObjectModel;

namespace amcs_scanner_app.View;

public partial class ShowItemPiece : ContentPage
{
    private PickingOrderItemsPage _page;
    private ResultItem _resultItem;
    private ObservableCollection<ResultItemStoragePlace> _storagePlaces;
    private AppSettings _settings;
    private ButtonPrintingItem buttonPrinting;
    private ResultItemStoragePlace resultISP;
    private static App app => Application.Current as App;
    private readonly BarcodeParser _barcodeParser;

    public ShowItemPiece(WarehouseClient warehouse, AppSettings settings, PickingOrderItemsPage page)
    {
        InitializeComponent();
        _barcodeParser = new BarcodeParser(settings);
        _page = page;
        _settings = settings;
        if (_settings.DisplayHeight > 600) ListStoragePlaces.HeightRequest = _settings.DisplayHeight * 0.6;
        else
        {
            ViewItemName.MaximumHeightRequest = 75;
            ListStoragePlaces.HeightRequest = _settings.DisplayHeight * 0.5;
        }
        SendItemBtn.HeightRequest = _settings.DisplayHeight * 0.08;
        if (_settings.IsPrintPerPosition) buttonPrinting = new ButtonPrintingItem(this, warehouse, _resultItem, GridStorages, 1, 0);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _resultItem = App.ResultItem;
        app.ScannerScannedEvent += OnScannerScanned;
        Successful.BackgroundColor = App.GetColorResource("StatusIncorrect");
        UnitName.Text = _resultItem.UnitName;
        ItemName.Text = _resultItem.ItemName;
        Amount.Text = _resultItem.Amount.ToString();
        RemnantAmount.Text = _resultItem.Amount.ToString();
        AmountUnit.Text = _resultItem.AmountUnit;
        Position.Text = _resultItem.Position.ToString();
        _storagePlaces = new ObservableCollection<ResultItemStoragePlace>();
        foreach (var item in _resultItem.ItemStoragePlaces)
        {
            _storagePlaces.Add(new ResultItemStoragePlace(item));
        }

        ListStoragePlaces.ItemsSource = _storagePlaces;
        SendItemBtn.IsEnabled = true;
    }


    private void CloseItem(object sender, EventArgs e)
    {
        CloseModal();
    }

    private void CloseModal()
    {
        app.ScannerScannedEvent -= OnScannerScanned;
        HideKeybrd();
        App.OrderItemsPageReload = false;
        Shell.Current.GoToAsync(nameof(PickingOrderItemsPage));
        //Navigation.PopModalAsync();
    }

    private void ListStoragePlaces_Tapped(object sender, EventArgs e)
    {
        var element = sender as VisualElement;
        var storageId = element.ClassId;
        StoragePlacesSelected(storageId);
    }

    private void StoragePlacesSelected(string storageId)
    {
        resultISP = null;
        foreach (var item in _storagePlaces)
        {
            if (storageId.Equals(item.StorageId)) resultISP = item;
            item.IsSelectedPlace = false;
        }

        if (resultISP == null) return;

        if (int.Parse(RemnantAmount.Text) != 0)
        {
            resultISP.IsSelectedPlace = true;

            if (int.Parse(resultISP.PlaceValue) != 0) resultISP.CurrentValue = (int.Parse(resultISP.PlaceValue) + int.Parse(RemnantAmount.Text)).ToString();
            else resultISP.CurrentValue = RemnantAmount.Text;

        }
        else if (int.Parse(resultISP.PlaceValue) != 0) { resultISP.IsSelectedPlace = true; }
    }


    private void StorageAdd(object sender, EventArgs e)
    {

        ResultItemStoragePlace resultISP = null;
        var entry = sender as Entry;
        // !!! entry.ClassId - StorageId
        foreach (var item in _storagePlaces)
        {
            if (entry.ClassId.Equals(item.StorageId)) resultISP = item;
        }

        if (resultISP == null || resultISP.CurrentValue == null) return;

        var temp = resultISP.CurrentValue;
        if (temp.Length > 1 && temp[0] == '0')
        {
            resultISP.CurrentValue = temp.Remove(0, 1);
            return;
        }

        var tempValue = resultISP.PlaceValue;

        char[] delimiterChars = { ',', '.' };
        string[] dec = resultISP.CurrentValue.Split(delimiterChars);
        decimal testAmount = TestDecimalPiece(dec[0]);

        if (!resultISP.CurrentValue.Equals(dec[0]))
        {
            //DisplayAlert("Die Eingabe ist mit einem Punkt oder einem Komma!", "", "Ok");
            if (dec[0] == "") resultISP.CurrentValue = "";
            else resultISP.CurrentValue = tempValue;
            return;
        }

        if (testAmount < 0)
        {
            //DisplayAlert("Die Eingabe ist fehlerhaft!", "", "Ok");
            resultISP.CurrentValue = tempValue;
            return;
        }

        resultISP.PlaceValue = testAmount.ToString();

        decimal currentAmount = CurrentAmount();

        if ((_resultItem.Amount - currentAmount) < 0)
        {
            DisplayAlert("Die Eingabe ist zu viel!", "", "Ok");
            resultISP.CurrentValue = tempValue;
            return;
        }

        var remains = _resultItem.Amount - currentAmount;
        CheckStatus(remains);
        RemnantAmount.Text = remains.ToString();
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

    private decimal CurrentAmount()
    {
        decimal currentAmount = 0;
        foreach (ResultItemStoragePlace item in _storagePlaces)
        {
            currentAmount += decimal.Parse(item.PlaceValue);
        }
        return currentAmount;
    }

    private void CheckStatus(decimal? remains)
    {
        if (remains == 0) Successful.BackgroundColor = App.GetColorResource("StatusFinish");
        else Successful.BackgroundColor = App.GetColorResource("StatusIncorrect");

        if (remains != _resultItem.Amount)
        {
            SendItemBtn.IsVisible = true;
        }
        else
        {
            SendItemBtn.IsVisible = false;
        }
    }

    private void SendPiece(object sender, EventArgs e)
    {
        SendItemBtn.IsEnabled = false;
        SendPiece();
    }

    private void SendPiece()
    {
        CloseModal();
        _page.Send(_storagePlaces, _resultItem.Id);
    }

    private void Entry_Completed(object sender, EventArgs e)
    {
        CloseModal();
        _page.Send(_storagePlaces, _resultItem.Id);
    }

    private void HideKeybrd()
    {
#if ANDROID
        if (Platform.CurrentActivity.CurrentFocus != null)
            Platform.CurrentActivity.HideKeyboard(Platform.CurrentActivity.CurrentFocus);
#endif
    }



    /*private void LayoutSizeChanged(object sender, EventArgs e)
    {
        if (Platform.CurrentActivity.CurrentFocus != null)
        {
            ListStoragePlaces.HeightRequest = _settings.DisplayHeight * 0.15;
        }
        else
        {
            ListStoragePlaces.HeightRequest = _settings.DisplayHeight * 0.5;

        }
    }*/
    private void ShowWebView(object sender, EventArgs e)
    {
        CloseListItemBtn.IsEnabled = false;
        ShowWebViewBtn.IsEnabled = false;
        ListStoragePlaces.IsEnabled = false;
        SendItemBtn.IsEnabled = false;
        SendItemBtn.Opacity = 0.4;
        if (buttonPrinting != null)
        {
            buttonPrinting.Opacity = 0.4;
            buttonPrinting.IsEnabled = false;
        }
        if (resultISP != null) resultISP.IsSelectedPlace = false;

        var text1 = _resultItem.ItemName;
        var text2 = _resultItem.Amount;
        var text3 = _resultItem.AmountUnit;
        HTMLWebView.Source = new HtmlWebViewSource
        {
            Html = @"<HTML>
                        <BODY>
                            <H3>" + text1 + @"</H3>
                            <P>" + text2 + " " + text3 + @"</P> 
                            <P id=""demo""> Demo </P>
                            <a href=""javascript:test1()"">Test1</a>
                            <a href=""javascript:test2()"">Test2</a>
                            <script type='text/javascript'>
                    
                                function test1() {
                                    document.getElementById(""demo"").innerHTML =""1. Test 11111"";
                                }
                                function test2() {
                                    document.getElementById(""demo"").innerHTML =""2. Test 22222"";
                                }

                            </script>
                        </BODY>
                    </HTML>"
        };
        ViewHTMLWebView.IsVisible = true;
    }
    private void CloseWebView(object sender, EventArgs e)
    {
        ViewHTMLWebView.IsVisible = false;
        HTMLWebView.ClearValue(WebView.SourceProperty);

        CloseListItemBtn.IsEnabled = true;
        ShowWebViewBtn.IsEnabled = true;
        ListStoragePlaces.IsEnabled = true;
        SendItemBtn.IsEnabled = true;
        SendItemBtn.Opacity = 0.8;
        if (buttonPrinting != null)
        {
            buttonPrinting.Opacity = 0.8;
            buttonPrinting.IsEnabled = true;
        }
        if (resultISP != null) resultISP.IsSelectedPlace = true;
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

            if (!(code.Identifier == _settings.ApplicationIdentifiers.Storage.Identifier))
                throw new InvalidOperationException("No Storage!");

            if (!_storagePlaces.Any(x => x.StorageId.ToString() == code.Value))
                throw new InvalidOperationException("Wrong Storage!");

            if (_resultItem.Weighing == 1)
            {
                return;
            }

            ListStoragePlaces.SelectedItem = _storagePlaces.First(x => x.StorageId.ToString() == code.Value);
            StoragePlacesSelected(code.Value);

            if (!_settings.PerformPickingWithScan)
            {
                StoragePlacesSelected(code.Value);
            }
            else
            {
                StoragePlacesSelected(code.Value);
                SendPiece();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
        }
    }

}