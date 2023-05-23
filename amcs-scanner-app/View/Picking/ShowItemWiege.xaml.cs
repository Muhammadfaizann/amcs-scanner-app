using amcs_scanner_app.Client;
using amcs_scanner_app.Helper;
using amcs_scanner_app.Resources.Localization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using System.Collections.ObjectModel;


namespace amcs_scanner_app.View;

public partial class ShowItemWiege : ContentPage
{
    private PickingOrderItemsPage _page;
    private ResultItem _resultItem;
    private ObservableCollection<ResultItemStoragePlace> _storagePlaces;
    private decimal _differencePercent;
    private AppSettings _settings;
    private ButtonPrintingItem buttonPrinting;
    private static App app => Application.Current as App;
    private readonly BarcodeParser _barcodeParser;
    public ShowItemWiege(PickingOrderItemsPage page, WarehouseClient warehouse, AppSettings settings)
    {
        InitializeComponent();
        _barcodeParser = new BarcodeParser(settings);
        _page = page;
        _differencePercent = settings.DifferencePercent;
        _settings = settings;

        StoragePlaceResetBtn.HeightRequest = _settings.DisplayHeight * 0.08;
        SendItemBtnWiege.HeightRequest = _settings.DisplayHeight * 0.08;
        if (_settings.DisplayHeight < 600) ViewItemName.MaximumHeightRequest = 75;
        if (_settings.IsPrintPerPosition) buttonPrinting = new ButtonPrintingItem(this, warehouse, _resultItem, GridStoragesWiege, 1, 0);

    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        app.ScannerScannedEvent += OnScannerScanned;
        _resultItem = App.ResultItem;
        UnitNameWiege.Text = _resultItem.UnitName;
        ItemNameWiege.Text = _resultItem.ItemName;
        AmountWiege.Text = "" + _resultItem.Amount;
        RemnantAmountWiege.Text = "" + _resultItem.Amount;
        AmountUnitWiege.Text = _resultItem.AmountUnit;
        PositionWiege.Text = "" + _resultItem.Position;
        AddStoragePlaces();
        //_resultItem.IsSelectedItem = true;
        SendItemBtnWiege.IsEnabled = true;
    }

    private void AddStoragePlaces()
    {
        _storagePlaces = new ObservableCollection<ResultItemStoragePlace>();
        foreach (var item in _resultItem.ItemStoragePlaces)
        {
            _storagePlaces.Add(new ResultItemStoragePlace(item) { IsSelectedPlace = true });
        }

        StoragePlacesWiege.Clear();
        //HeightRequest = Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfo.Height * 0.3
        if (_settings.DisplayHeight > 600) ScrollStoragePlaces.MaximumHeightRequest = _settings.DisplayHeight * 0.45;
        else if (_settings.DisplayHeight > 500) ScrollStoragePlaces.MaximumHeightRequest = _settings.DisplayHeight * 0.28;
        else ScrollStoragePlaces.MaximumHeightRequest = _settings.DisplayHeight * 0.2;


        foreach (var item in _storagePlaces)
        {
            VerticalStackLayout verticalStackLayout = new VerticalStackLayout
            {
                ClassId = item.StorageId
            };

            var label = new Label
            {
                HorizontalTextAlignment = Microsoft.Maui.TextAlignment.Start,
                VerticalTextAlignment = Microsoft.Maui.TextAlignment.Center,
                FontSize = 18,
                Text = item.StorageId
            };

            verticalStackLayout.Add(label);

            var listView = new ListView
            {
                //VerticalScrollBarVisibility = ScrollBarVisibility.Never,
                SelectionMode = ListViewSelectionMode.None,
            };
            listView.ItemsSource = item.WeightPlaces;
            listView.SetBinding(ListView.IsVisibleProperty, "IsSelectedPlace");

            // Erstellen eines DataTemplates für die Listenelemente
            var dataTemplate = new DataTemplate(() =>
            {
                var column0 = new ColumnDefinition();
                var column1 = new ColumnDefinition();
                var column2 = new ColumnDefinition();

                column1.Width = 70;
                column2.Width = 80;

                var grid = new Grid
                {
                    Margin = new Thickness(0, 2.5, 0, 2.5),
                    HeightRequest = 40,
                    VerticalOptions = new LayoutOptions(LayoutAlignment.Center, false),
                    HorizontalOptions = LayoutOptions.Fill,
                };
                grid.AddColumnDefinition(column0);
                grid.AddColumnDefinition(column1);
                grid.AddColumnDefinition(column2);

                var entry = new Entry
                {
                    Placeholder = "bitte Gewicht eingeben",
                    Keyboard = Keyboard.Numeric,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 18,

                };
                entry.SetBinding(Entry.ClassIdProperty, "PlaceIndex");
                entry.SetBinding(Entry.TextProperty, "PlaceValue");
                entry.SetBinding(Entry.IsEnabledProperty, "IsActive");
                entry.SetValue(Entry.IsPasswordProperty, false);
                entry.SetValue(Entry.BackgroundColorProperty, "{AppThemeBinding Light=White,Dark={StaticResource Gray300}}");
                entry.SetValue(Entry.TextColorProperty, "{AppThemeBinding Light={StaticResource Black},Dark={StaticResource White}}");
                entry.Completed += (sender, e) =>
                {
                    AddWeightPlace(sender, verticalStackLayout.ClassId);
#if ANDROID
                    if(Platform.CurrentActivity.CurrentFocus != null)
                    Platform.CurrentActivity.HideKeyboard(Platform.CurrentActivity.CurrentFocus);
#endif
                };

                item.Entry = entry;

                var btnAdd = new Button
                {
                    Opacity = 0.8,
                    CornerRadius = 20,
                    HeightRequest = 60,
                    WidthRequest = 60,
                    Text = "+",
                    FontSize = 18,
                    TextColor = Colors.White,
                    Margin = new Thickness(15, 0, 10, 0),
                    HorizontalOptions = LayoutOptions.Start
                };

                btnAdd.SetBinding(Button.ClassIdProperty, "PlaceIndex");
                btnAdd.SetBinding(Button.IsEnabledProperty, "IsActive");
                btnAdd.SetValue(Button.BackgroundColorProperty, "{StaticResource Blue100Accent}");
                btnAdd.SetValue(Button.VerticalOptionsProperty, "Center");

                btnAdd.Clicked += (sender, args) =>
                {
                    AddWeightPlace(sender, verticalStackLayout.ClassId);
                };

                string source = "trash_can_dark.png";
                if (App.Current.UserAppTheme == AppTheme.Dark) source = "trash_can_dark.png";
                var btnDel = new ImageButton
                {
                    Source = source,
                    BackgroundColor = App.GetColorResource("Blue100Accent"),
                    Opacity = 0.8,
                    CornerRadius = 5,
                    HeightRequest = 60,
                    WidthRequest = 70,
                    Padding = new Thickness(5, 10, 5, 10),
                    HorizontalOptions = LayoutOptions.End
                };
                btnDel.SetBinding(ImageButton.ClassIdProperty, "PlaceIndex");
                btnDel.SetBinding(Button.IsEnabledProperty, "IsActive");

                btnDel.Clicked += (sender, args) =>
                {
                    DeleteWeightPlace(sender, verticalStackLayout.ClassId);
                };

                grid.Add(entry);
                grid.Add(btnAdd);
                grid.Add(btnDel);
                grid.SetColumn(entry, 0);
                grid.SetColumn(btnAdd, 1);
                grid.SetColumn(btnDel, 2);


                return new ViewCell
                {
                    View = grid
                };
            });

            listView.ItemTemplate = dataTemplate;

            verticalStackLayout.Add(listView);

            StoragePlacesWiege.Add(verticalStackLayout);

        }
        SuccessfulWiege.BackgroundColor = App.GetColorResource("StatusIncorrect");
    }


    private void CloseItemWiege(object sender, EventArgs e)
    {
        CloseModalWiege();
    }

    private void CloseModalWiege()
    {
        app.ScannerScannedEvent -= OnScannerScanned;
        App.OrderItemsPageReload = false;
        Shell.Current.GoToAsync(nameof(PickingOrderItemsPage));
        //Navigation.PopModalAsync();
    }

    private void DeleteWeightPlace(object sender, string classId)
    {
        ImageButton button = sender as ImageButton;

        ResultItemStoragePlace resultISP = null;
        foreach (var item in _storagePlaces)
        {
            if (item.StorageId.Equals(classId)) resultISP = item;
        }
        var weightPlaces = resultISP.WeightPlaces;
        int index = int.Parse(button.ClassId);

        if (weightPlaces.Count == 1)
        {
            resultISP.IsSelectedPlace = false;
            return;
        }
        if (index == (weightPlaces.Count - 1)) return;
        weightPlaces.Remove(weightPlaces[index]);

        if (weightPlaces.Count > 0)
        {
            for (int i = 0; i < weightPlaces.Count; i++)
            {
                weightPlaces[i].PlaceIndex = "" + i;
            }
        }

        decimal currentAmount = 0;
        foreach (var item in _storagePlaces)
        {
            currentAmount += item.GetWeightAmount();
        }

        decimal remains = (decimal)_resultItem.Amount - currentAmount;
        decimal difference = (decimal)_resultItem.Amount * _differencePercent;

        // uint speedUp = 500;
        if (remains > (-difference) && remains < difference)
        {
            SuccessfulWiege.BackgroundColor = App.GetColorResource("StatusFinish");
            // SendItemBtnWiege.TranslateTo(0, -150, speedUp, Easing.Linear);
            SendItemBtnWiege.IsVisible = true;
        }
        else
        {
            SuccessfulWiege.BackgroundColor = App.GetColorResource("StatusIncorrect");
            // SendItemBtnWiege.TranslateTo(0, 150, speedUp, Easing.Linear);
            SendItemBtnWiege.IsVisible = false;
        }

        RemnantAmountWiege.Text = remains.ToString();
    }

    private void AddWeightPlace(object sender, string classId)
    {
        VisualElement element = sender as VisualElement;

        ResultItemStoragePlace resultISP = null;
        foreach (var item in _storagePlaces)
        {
            if (item.StorageId.Equals(classId)) resultISP = item;
        }
        var weightPlaces = resultISP.WeightPlaces;

        // !!! element.ClassId - PlaceIndex
        var placeValue = weightPlaces[int.Parse(element.ClassId)].PlaceValue.Replace(",", ".");


        if (placeValue == "")
        {
            DisplayAlert("Keine Eingabe.", "", "Ok");
            return;
        }

        decimal testAmount = TestDecimal(placeValue);
        if (testAmount < 0)
        {
            DisplayAlert("Eingabe ist fehlerhaft.", "", "Ok");
            return;
        }
        if (testAmount == 0)
        {
            DisplayAlert("Die Eingabe ist 0.", "", "Ok");
            return;
        }

        decimal currentAmount = 0;
        foreach (var item in _storagePlaces)
        {
            currentAmount += item.GetWeightAmount();
        }

        decimal remains = (decimal)_resultItem.Amount - currentAmount;
        decimal difference = (decimal)_resultItem.Amount * _differencePercent;

        if (remains < (-difference))
        {
            DisplayAlert("Die Eingabe ist zu viel!", "", "Ok");
            return;
        }

        //uint speedUp = 500;

        if (remains >= (-difference) && remains <= difference)
        {
            SendItemBtnWiege.IsVisible = true;
            // SendItemBtnWiege.TranslateTo(0, -150, speedUp, Easing.Linear);
            SuccessfulWiege.BackgroundColor = App.GetColorResource("StatusFinish");
        }
        else
        {
            SendItemBtnWiege.IsVisible = false;
            // SendItemBtnWiege.TranslateTo(0, 150, speedUp, Easing.Linear);
            SuccessfulWiege.BackgroundColor = App.GetColorResource("StatusIncorrect");
        }

        RemnantAmountWiege.Text = remains.ToString();
        weightPlaces.Add(new WeightPlace(resultISP.StorageId, weightPlaces.Count.ToString()));
        element.IsEnabled = false;

        //Eingabefeld
        var grid = element.Parent as Grid;
        if (sender.GetType().Equals(typeof(Button)))
        {
            var entry = grid.Children[0] as VisualElement;
            entry.IsEnabled = false;
        }
        else
        {
            var button = grid.Children[1] as VisualElement;
            button.IsEnabled = false;
        }

    }

    private decimal TestDecimal(string value)
    {
        if (value == null || value == "") return 0;

        if (decimal.TryParse(value, out decimal number))
        {
            if (number >= 0)
                return number;
        }
        return -1;
    }

    private void StoragePlaceResetBtn_Clicked(object sender, EventArgs e)
    {
        RemnantAmountWiege.Text = "" + _resultItem.Amount;
        AddStoragePlaces();
    }

    private void SendWiege(object sender, EventArgs e)
    {
        SendItemBtnWiege.IsEnabled = false;
        CloseModalWiege();
        _page.Send(_storagePlaces, _resultItem.Id);
    }

    private void ShowWebView(object sender, EventArgs e)
    {
        CloseListItemBtnWiege.IsEnabled = false;
        ShowWebViewBtn.IsEnabled = false;
        foreach (var place in _storagePlaces)
        {
            foreach (var item in place.WeightPlaces)
            {
                item.IsActive = false;
            }
        }
        StoragePlaceResetBtn.IsEnabled = false;
        SendItemBtnWiege.IsEnabled = false;
        if (buttonPrinting != null) buttonPrinting.IsEnabled = false;

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

        CloseListItemBtnWiege.IsEnabled = true;
        ShowWebViewBtn.IsEnabled = true;
        foreach (var place in _storagePlaces)
        {
            foreach (var item in place.WeightPlaces)
            {
                item.IsActive = true;
            }
        }
        StoragePlaceResetBtn.IsEnabled = true;
        SendItemBtnWiege.IsEnabled = true;
        if (buttonPrinting != null) buttonPrinting.IsEnabled = true;
    }

    private async void OnScannerScanned(object sender, ScannerEventArgs e)
    {
        /*var scan = e.Content;
        var codesScanned = new List<BarcodeContent>();
        try
        {
            codesScanned = _barcodeParser.ReadBarcode(scan);
            if (!codesScanned.Any())
                throw new InvalidOperationException(AppResources.NoBarcodeParsed);

            if (codesScanned.Count != 1)
                throw new InvalidOperationException(AppResources.MoreThanOneCodeScanned);

            var code = codesScanned[0];

            var position = _resultItem;

            if (!(code.Identifier == _settings.ApplicationIdentifiers.Storage.Identifier))
                throw new InvalidOperationException("No Storage!");

            if (!_storagePlaces.Any(x => x.StorageId.ToString() == code.Value))
                throw new InvalidOperationException("Wrong Storage!");

            ListView list = null;

            foreach (var child in StoragePlacesWiege.Children)
            {
                var vsl = child as VerticalStackLayout;
                if (vsl != null && vsl.ClassId.Equals(code.Value))
                {
                    list = vsl.Children[1] as ListView;
                    
                }
            }

            //ResultItemStoragePlace place = list.ItemsSource as ResultItemStoragePlace;
    
            foreach (var item in _storagePlaces)
            {
                if (item.StorageId.Equals(code.Value)) 
                {
                    list.SelectedItem = item.WeightPlaces[item.WeightPlaces.Count - 1];
                    item.Entry.Focus();
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("test " + AppResources.Error, ex.Message, AppResources.Ok);
        }*/
    }

}