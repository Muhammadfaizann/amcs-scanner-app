using amcs_scanner_app.Client;
using amcs_scanner_app.Helper;
using amcs_scanner_app.Handler;
using amcs_shared_lib.Enum;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using amcs_shared_lib.Models.DTO;
using amcs_shared_lib.Models.HTTP;
using amcs_scanner_app.Resources.Localization;
using amcs_scanner_app.View.Inventory;

namespace amcs_scanner_app.View;

public partial class PickingOrderPage : ContentPage
{
    private readonly BarcodeParser _barcodeParser;
    private readonly WarehouseClient _warehouse;
    private HubConnection _ordersHubConnection;
    private readonly PickingHandler _handler;
    private int _maxSelected;
    private ObservableCollection<ResultOrder> Results { get; set; }

    private AppSettings _settings;
    private bool IsRightPlaceSelectedBtn;
    private static App App => Application.Current as App;

    private int counter = 0;
    private int sortDateCounter = 0;
    private int sortTourCounter = 0;
    private int sortNumberCounter = 0;
    public PickingOrderPage(WarehouseClient warehouse, AppSettings settings, PickingHandler handler)
    {
        _handler = handler;
        _handler.Start();
        _settings = settings;
        _maxSelected = settings.MaxCombinedOrders;
        Results = new ObservableCollection<ResultOrder>();
        InitializeComponent();
        _barcodeParser = new BarcodeParser(settings);
        _warehouse = warehouse;

        
        ListOrders.HeightRequest = _settings.DisplayHeight * 0.85;
        if (_settings.DisplayHeight > 600) ListPickingOrders.MaximumHeightRequest = _settings.DisplayHeight * 0.7;
        else if (_settings.DisplayHeight > 500) ListPickingOrders.MaximumHeightRequest = _settings.DisplayHeight * 0.65;
        else ListPickingOrders.MaximumHeightRequest = _settings.DisplayHeight * 0.6;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (App.OrderPageReload) 
        {
            counter = 0;
            sortDateCounter = 0;
            sortTourCounter = 0;
            sortNumberCounter = 0;
            IsRightPlaceSelectedBtn = true;
            ViewSelectedOrders.IsVisible = false;
            ListOrders.IsVisible = true;
            ButtonsViewElement.IsVisible = true;
            ShowSelectedOrdersBtn.IsVisible = false;
            ShowSelectedOrdersBtn.TranslateTo(0, _settings.DisplayHeight / 3, 0, Easing.Linear);
            Setup();
        }
        App.ScannerScannedEvent += OnScannerScanned;
    }

    #region Spinner/Flyout
    public ActivityIndicator SpinningStart()
    {
        ListOrders.IsVisible = false;
        ButtonsViewElement.IsVisible = false;
        Spinner.IsVisible = true;
        Spinner.IsRunning = true;

        return new ActivityIndicator { IsRunning = true, Color = Colors.Orange };
    }

    public ActivityIndicator SpinningEnd()
    {
        Spinner.IsVisible = false;
        Spinner.IsRunning = false;
        ListOrders.IsVisible = true;
        ButtonsViewElement.IsVisible = true;
        return new ActivityIndicator { IsRunning = false, Color = Colors.Orange };
    }

    private void FlyoutOpen(object sender, EventArgs e)
    {
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Flyout;
        Shell.Current.FlyoutIsPresented = true;
    }
    #endregion

    #region Setup und Eventhandler
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
                if (!(code.Identifier == _settings.ApplicationIdentifiers.Order.Identifier))
                    throw new InvalidOperationException(AppResources.ScannedCodeWasNoOrder);

                if (!Results.Any(x => x.OrderId.ToString() == code.Value && x.IsActive))
                    throw new InvalidOperationException(AppResources.ScannedOrderNotInList);

                ListOrders.SelectedItem = Results.First(x => x.OrderId.ToString() == code.Value && x.IsActive);

                CheckList();
            }
            else
            {
                throw new Exception(AppResources.MoreThanOneCodeScanned);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
        }
    }

    public async void Setup()
    {
        SpinningStart();
        List<PickingOrderOverviewDTO> orders;
        int maxtries = 5;
        int iterations = 0;
        do
        {
            iterations++;
            orders = await GetOrders();
        } while (orders is null && maxtries > iterations);

        if (orders is null)
        {
            // Keine Verbingung -> App neu laden
            await DisplayAlert(AppResources.Error, AppResources.NoConnectionToSystem, AppResources.Ok);
            Application.Current.MainPage = new AppShell();
            return;
        }

        CreateItems(orders);

        var token = await SecureStorage.GetAsync("BearerToken");

        _ordersHubConnection = new HubConnectionBuilder()
            .WithUrl(_settings.WarehouseSettings.BaseUrl + _settings.WarehouseSettings.PickingOrderStatusHub, opt =>
            {
                opt.Headers.Add("Authorization", "Bearer " + token);
            })
            .Build();

        _ordersHubConnection.On<string>("PickingOrderStatusChange", (message) =>
        {
            var statusChange = JsonSerializer.Deserialize<SetPicklistRequest>(message);

            UpdateStatus(statusChange);
        });

        await _ordersHubConnection.StartAsync();

        _ordersHubConnection.Closed += async (error) =>
        {
            while (_ordersHubConnection.State != HubConnectionState.Connected)
            {
                // Wait for a short period of time before attempting to reconnect
                await Task.Delay(5000);

                // Attempt to reconnect
                await _ordersHubConnection.StartAsync();
            }
        };
        // Spinner deaktivieren
        SpinningEnd();
    }

    public void UpdateStatus(SetPicklistRequest change)
    {
        var order = Results.FirstOrDefault(x => x.Id == change.Id);

        if (order != null)
        {
            Results.FirstOrDefault(x => x.Id == change.Id).Status = (short)change.OrderStatus;
            //Results = new ObservableCollection<ResultOrder>(Results);
            //ListOrders.ItemsSource = Results;
        }
    }

    public async Task<List<PickingOrderOverviewDTO>> GetOrders()
    {
        try
        {
            return await _warehouse.GetPicklistOverview();
        }
        catch (Exception e)
        {
            return new List<PickingOrderOverviewDTO>();
        }
    }

    private void CreateItems(List<PickingOrderOverviewDTO> orderDTOs)
    {
        Results.Clear();
        for (int i = 0; i < orderDTOs.Count; i++)
        {
            Results.Add(new ResultOrder(orderDTOs[i]));
        }
        Results.ToList().RemoveAll(x => x.ItemCount < 1);
        RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderByDescending(x => x.IsActive)));
        ListOrders.ItemsSource = Results;
    }
    #endregion

    #region Auswahl des Auftrags
    private async void ListOrders_ItemTapped(object sender, EventArgs e)
    {
        if (!IsRightPlaceSelectedBtn) return;
        if (counter < 0)
        {
            //hinzuf�gen eine Exception
            await DisplayAlert("Fehler", "Counter: " + counter, "Ok");
            return;
        }

        var viewCell = sender as ViewCell;
        ResultOrder result = Results[int.Parse(viewCell.ClassId) - 1];

        if (!result.IsActive) return;

        if (counter == _maxSelected && !result.IsSelectedItem)
        {
            await DisplayAlert("Sie k�nnen nur " + _maxSelected + " Auftr�ge ausw�hlen", " ", "Ok");
            return;
        }

        if (_maxSelected == 1 && !result.IsSelectedItem)
        {
            var orders = new List<ResultOrder>();
            orders.Add(result);
            // order als begonnen melden
            try
            {
                await _warehouse.PostSetPicklist(new SetPicklistRequest
                {
                    Id = result.Id,
                    OrderStatus = amcs_shared_lib.Enum.EPickingOrderStatus.Begonnen
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
            }

            /*var parameter = new Dictionary<string, object>
                {
                    { "Orders", orders }
                };
            await Shell.Current.GoToAsync(nameof(PickingOrderItemsPage), parameter);*/
            App.OrderItemsPageReload = true;
            App.Orders = orders;
            await Shell.Current.GoToAsync(nameof(PickingOrderItemsPage));
            //await Shell.Current.Navigation.PushAsync(new PickingOrderItemsPage(orders, _warehouse, _settings, _handler));
            return;
        }
        ShowSelectedBtn(result);

    }

    private async void CheckList()
    {
        if (!IsRightPlaceSelectedBtn) return;
        if (counter < 0)
        {
            // ToDo: Seite neu laden..
            await DisplayAlert(AppResources.Error, "Counter: " + counter, AppResources.Ok);
            return;
        }

        ResultOrder result = ListOrders.SelectedItem as ResultOrder;

        if (result is null)
        {
            return;
        }

        if (!result.IsActive)
        {
            result.IsSelectedItem = false;
            return;
        }

        if (counter == _maxSelected && !result.IsSelectedItem)
        {
            Results.Last(x => x.IsSelectedItem).IsSelectedItem = false;
            result.IsSelectedItem = true;
            return;
        }

        if (_maxSelected == 1 && result.IsSelectedItem)
        {
            ShowSelected();
            return;
        }
        ShowSelectedBtn(result);

    }

    private async void ShowSelectedBtn(ResultOrder result)
    {
        uint speedUp = 150;
        var moveY = _settings.DisplayHeight / 3;
        if (result.IsSelectedItem)
        {
            counter--;

            if (counter < 1)
            {
                IsRightPlaceSelectedBtn = false;
                await ShowSelectedOrdersBtn.TranslateTo(0, moveY, speedUp, Easing.Linear);
                IsRightPlaceSelectedBtn = true;
                ShowSelectedOrdersBtn.IsVisible = false;
            }
            result.IsSelectedItem = false;
        }
        else
        {
            counter++;

            if (counter > 0)
            {
                ShowSelectedOrdersBtn.IsVisible = true;
                IsRightPlaceSelectedBtn = false;
                await ShowSelectedOrdersBtn.TranslateTo(0, -moveY, speedUp, Easing.Linear);
                IsRightPlaceSelectedBtn = true;
            }
            result.IsSelectedItem = true;
        }
    }

    private async void Start(object sender, EventArgs e)
    {
        StartBtn.IsEnabled = false;
        StartBtn.IsVisible = false;
        PrintBtn.IsVisible = false;
        try
        {
            var orders = await SetOrderStatusStarted();
            if (!(orders is null))
            {
                ListOrders.IsEnabled = true;
                ShowSelectedOrdersBtn.IsVisible = false;
                // Reset
                IsRightPlaceSelectedBtn = false;
                await ShowSelectedOrdersBtn.TranslateTo(0, 250, 150, Easing.Linear);
                IsRightPlaceSelectedBtn = true;
                App.ScannerScannedEvent -= OnScannerScanned;

                /*var parameter = new Dictionary<string, object>
                {
                    ["Orders"] = orders 
                };
                await Shell.Current.GoToAsync(nameof(PickingOrderItemsPage), parameter);*/
                App.OrderItemsPageReload = true;
                App.Orders = orders;
                await Shell.Current.GoToAsync(nameof(PickingOrderItemsPage));
                //await Shell.Current.Navigation.PushAsync(new PickingOrderItemsPage(orders, _warehouse, _settings, _handler));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
        }
        PrintBtn.IsVisible = _settings.IsPrintAtEnd;
    }
    #endregion

    #region Sortiermethoden
    private void RefreshItems(ObservableCollection<ResultOrder> tempResults)
    {
        for (int i = 0; i < tempResults.Count; i++)
        {
            tempResults[i].Position = i + 1;
        }
        Results = new ObservableCollection<ResultOrder>(tempResults);
        ListOrders.ItemsSource = Results;
    }
    private void SortDateAsc(object sender, EventArgs e)
    {
        if (sortDateCounter % 2 == 0)
        {
            if (App.Current.UserAppTheme == AppTheme.Light)
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderBy(x => x.GetDateOrder_yyyyMMdd())));
                SortDateAscBtnImage.IsVisible = true;
                SortDateAscBtnImage.Source = "arrowup_light.png";
                SortNumberArtAscBtnImage.IsVisible = false;
                SortTourNameAscBtnImage.IsVisible = false;
            }
            else
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderBy(x => x.GetDateOrder_yyyyMMdd())));
                SortDateAscBtnImage.IsVisible = true;
                SortDateAscBtnImage.Source = "arrowup_dark.png";
                SortNumberArtAscBtnImage.IsVisible = false;
                SortTourNameAscBtnImage.IsVisible = false;
            }

        }
        else
        {
            if (App.Current.UserAppTheme == AppTheme.Light)
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderByDescending(x => x.GetDateOrder_yyyyMMdd())));
                SortDateAscBtnImage.IsVisible = true;
                SortDateAscBtnImage.Source = "arrowdown_light.png";
                SortNumberArtAscBtnImage.IsVisible = false;
                SortTourNameAscBtnImage.IsVisible = false;
            }
            else
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderByDescending(x => x.GetDateOrder_yyyyMMdd())));
                SortDateAscBtnImage.IsVisible = true;
                SortDateAscBtnImage.Source = "arrowdown_dark.png";
                SortNumberArtAscBtnImage.IsVisible = false;
                SortTourNameAscBtnImage.IsVisible = false;
            }
        }
        sortDateCounter++;
    }

    private void SortNumberArtAsc(object sender, EventArgs e)
    {
        if (sortNumberCounter % 2 == 0)
        {
            if (App.Current.UserAppTheme == AppTheme.Light)
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderBy(x => x.ItemCount)));
                SortDateAscBtnImage.IsVisible = false;
                SortNumberArtAscBtnImage.IsVisible = true;
                SortNumberArtAscBtnImage.Source = "arrowup_light.png";
                SortTourNameAscBtnImage.IsVisible = false;
            }
            else
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderBy(x => x.ItemCount)));
                SortDateAscBtnImage.IsVisible = false;
                SortNumberArtAscBtnImage.IsVisible = true;
                SortNumberArtAscBtnImage.Source = "arrowup_dark.png";
                SortTourNameAscBtnImage.IsVisible = false;
            }
        }
        else
        {
            if (App.Current.UserAppTheme == AppTheme.Light)
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderByDescending(x => x.ItemCount)));
                SortDateAscBtnImage.IsVisible = false;
                SortNumberArtAscBtnImage.IsVisible = true;
                SortNumberArtAscBtnImage.Source = "arrowdown_light.png";
                SortTourNameAscBtnImage.IsVisible = false;
            }
            else
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderByDescending(x => x.ItemCount)));
                SortDateAscBtnImage.IsVisible = false;
                SortNumberArtAscBtnImage.IsVisible = true;
                SortNumberArtAscBtnImage.Source = "arrowdown_dark.png";
                SortTourNameAscBtnImage.IsVisible = false;
            }
        }
        sortNumberCounter++;
    }

    private void SortTourNameAsc(object sender, EventArgs e)
    {
        if (sortTourCounter % 2 == 0)
        {
            if (App.Current.UserAppTheme == AppTheme.Light)
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderBy(x => x.TourName)));
                SortDateAscBtnImage.IsVisible = false;
                SortNumberArtAscBtnImage.IsVisible = false;
                SortTourNameAscBtnImage.IsVisible = true;
                SortTourNameAscBtnImage.Source = "arrowup_light.png";
            }
            else
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderBy(x => x.TourName)));
                SortDateAscBtnImage.IsVisible = false;
                SortNumberArtAscBtnImage.IsVisible = false;
                SortTourNameAscBtnImage.IsVisible = true;
                SortTourNameAscBtnImage.Source = "arrowup_dark.png";
            }
        }
        else
        {
            if (App.Current.UserAppTheme == AppTheme.Light)
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderByDescending(x => x.TourName)));
                SortDateAscBtnImage.IsVisible = false;
                SortNumberArtAscBtnImage.IsVisible = false;
                SortTourNameAscBtnImage.IsVisible = true;
                SortTourNameAscBtnImage.Source = "arrowdown_light.png";
            }
            else
            {
                RefreshItems(new ObservableCollection<ResultOrder>(Results.OrderByDescending(x => x.TourName)));
                SortDateAscBtnImage.IsVisible = false;
                SortNumberArtAscBtnImage.IsVisible = false;
                SortTourNameAscBtnImage.IsVisible = true;
                SortTourNameAscBtnImage.Source = "arrowdown_dark.png";
            }
        }
        sortTourCounter++;
    }

    private void ShowSelectedOrders(object sender, EventArgs e)
    {
        ShowSelected();
    }

    private void ShowSelected()
    {
        var orders = GetSelectedOrders();
        ListPickingOrders.ItemsSource = orders;
        ViewSelectedOrders.IsVisible = true;
        StartBtn.IsVisible = true;
        StartBtn.IsEnabled = true;
        ListOrders.IsVisible = false;
        ButtonsViewElement.IsVisible = false;
        ShowSelectedOrdersBtn.IsVisible = false;

        PrintBtn.IsVisible = _settings.IsPrintAtStart;
    }

    private async void Print(object sender, EventArgs e)
    {
        EPrintLabelTypes typ;
        if (StartBtn.IsVisible) typ = EPrintLabelTypes.StartPicking;
        else typ = EPrintLabelTypes.EndPicking;

        var orders = ListPickingOrders.ItemsSource as List<ResultOrder>;
        if (orders != null)
            foreach (ResultOrder order in orders)
            {
                try
                {
                    await _warehouse.PostPrintPickingLabel(new PrintLabelRequest
                    {
                        Id = order.Id,
                        LabelType = typ
                    });
                }
                catch (Exception ex)
                {
                    await DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
                }
            }
        else await DisplayAlert("Error: No orders", "", "Ok");
    }

    private void CloseSelectedOrder(object sender, EventArgs e)
    {
        ViewSelectedOrders.IsVisible = false;
        RefreshItems(Results);
        ListOrders.IsVisible = true;
        ButtonsViewElement.IsVisible = true;
        ShowSelectedOrdersBtn.IsVisible = true;
    }

    public List<ResultOrder> GetSelectedOrders() =>
        Results.Where(x => x.IsSelectedItem).ToList();


    private async Task<List<ResultOrder>> SetOrderStatusStarted()
    {
        var orders = GetSelectedOrders();

        foreach (var item in orders)
        {
            item.IsSelectedItem = false;
            counter--;

            var response = await _warehouse.PostSetPicklist(new SetPicklistRequest
            {
                Id = item.Id,
                OrderStatus = EPickingOrderStatus.Begonnen
            });

            if (response is null)
            {
                throw new InvalidOperationException(AppResources.NoConnectionToSystem);
            }
        }
        return orders;
    }

    private void ShowWebView(object sender, EventArgs e)
    {
        ListOrders.IsEnabled = false;
        ShowSelectedOrdersBtn.IsEnabled = false;
        SortDateAscBtn.IsEnabled = false;
        SortTourNameAscBtn.IsEnabled = false;
        SortNumberArtAscBtn.IsEnabled = false;

        var button = sender as ImageButton;
        int index = int.Parse(button.ClassId) - 1;
        var text1 = Results[index].OrderId;
        var text2 = Results[index].Date;

        HTMLWebView.Source = new HtmlWebViewSource
        {
            Html = @"<HTML>
                        <BODY>
                            <H1>Auftrag " + text1 + @"</H1>
                            <P>Datum: " + text2 + @"</P> 
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

        ListOrders.IsEnabled = true;
        ShowSelectedOrdersBtn.IsEnabled = true;
        SortDateAscBtn.IsEnabled = true;
        SortTourNameAscBtn.IsEnabled = true;
        SortNumberArtAscBtn.IsEnabled = true;
    }
}
#endregion

#region ViewCell Model
public class ResultOrder : INotifyPropertyChanged
{
    public long Id { get; private set; }
    public long OrderId { get; private set; }
    public string TourName { get; private set; }
    public string StatusName { get; private set; }
    public string Date { get; private set; }

    private short status;
    public short Status
    {
        get => status;
        set
        {
            status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            switch (status)
            {
                case 0:
                    ColorStatus = App.GetColorResource("StatusNew");//Neu
                    StatusName = AppResources.New;
                    IsActive = false;
                    break;
                case 1:
                    ColorStatus = App.GetColorResource("StatusReceived");//Abgeholt
                    StatusName = AppResources.New;
                    IsActive = true;
                    break;
                case 2:
                    ColorStatus = App.GetColorResource("StatusStarted");//Begonnen
                    StatusName = AppResources.Started;
                    IsActive = false;
                    break;
                case 3:
                    ColorStatus = App.GetColorResource("StatusInterrupted");//Unterbrochen
                    StatusName = AppResources.Paused;
                    IsActive = true;
                    break;
                case 4:
                    ColorStatus = App.GetColorResource("StatusFinish");//Abgeschlossen
                    StatusName = AppResources.Finish;
                    IsActive = false;
                    break;
                case 5:
                    ColorStatus = App.GetColorResource("StatusDeleted");//Gel�scht
                    StatusName = AppResources.Deleted;
                    IsActive = false;
                    break;
                case 9:
                    ColorStatus = App.GetColorResource("StatusIncorrect");//Fehlerhaft
                    StatusName = AppResources.Incorrect;
                    IsActive = false;
                    break;
                default:
                    ColorStatus = App.GetColorResource("StatusIncorrect");
                    IsActive = false;
                    break;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusName)));
        }
    }

    private Color colorStatus;
    public Color ColorStatus
    {
        get => colorStatus;
        set
        {
            colorStatus = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorStatus)));
        }
    }

    private bool isActive;
    public bool IsActive
    {
        get => isActive;
        set
        {
            isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));

            if (IsActive) Opacitylevel = 1;
            else Opacitylevel = 0.8;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Opacitylevel)));
        }
    }

    public long PickingOrderId { get; private set; }
    public string ItemId { get; private set; }
    public int ItemCount { get; private set; }

    private bool isSelectedItem;
    public bool IsSelectedItem
    {
        get => isSelectedItem;
        set
        {
            if (!isActive) return;

            isSelectedItem = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectedItem)));

            if (isSelectedItem)
            {
                if (App.Current.UserAppTheme == AppTheme.Light)
                {
                    colorBackgrItem = App.GetColorResource("Gray600"); // Background Selected Day - DarkGrey;
                    colorTextItem = App.GetColorResource("White"); //Text Selected Day - White;
                }
                else
                {
                    colorBackgrItem = App.GetColorResource("White"); // Background Selected Night - White
                    colorTextItem = App.GetColorResource("Black"); //Text Selected Night - Black
                }
            }
            else
            {
                if (App.Current.UserAppTheme == AppTheme.Light)
                {

                    colorBackgrItem = App.GetColorResource("White"); // Background Selected Night - White
                    colorTextItem = App.GetColorResource("Black"); //Text Selected Night - Black

                }
                else
                {
                    colorBackgrItem = App.GetColorResource("Gray600"); // Background Selected Day - DarkGrey;
                    colorTextItem = App.GetColorResource("White"); //Text Selected Day - White;
                }
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorBackgrItem)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorTextItem)));
        }
    }

    private Color colorTextItem;
    public Color ColorTextItem
    {
        get => colorTextItem;
        set
        {
            colorTextItem = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorTextItem)));
        }
    }

    private Color colorBackgrItem;
    public Color ColorBackgrItem
    {
        get => colorBackgrItem;
        set
        {
            colorBackgrItem = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorBackgrItem)));
        }
    }

    private double opacitylevel;
    public double Opacitylevel
    {
        get => opacitylevel;
        set
        {
            opacitylevel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Opacitylevel)));
        }
    }
    private int position;
    public int Position
    {
        get => position;
        set
        {
            position = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
        }
    }

    public ResultOrder(PickingOrderOverviewDTO pickingOverviewDTO)
    {
        Date = String.Format("{0:dd.MM.yyyy}", pickingOverviewDTO.DepartureTime);
        Id = pickingOverviewDTO.Id;
        OrderId = pickingOverviewDTO.OrderId;
        TourName = pickingOverviewDTO.TourName;
        Status = pickingOverviewDTO.Status;
        StatusName = GetStatusName((EPickingOrderStatus)Status);
        ItemCount = pickingOverviewDTO.ItemCount;
        IsSelectedItem = false;
    }

    public string GetStatusName(EPickingOrderStatus status) =>
        status switch
        {
            EPickingOrderStatus.Abgeholt => AppResources.Retrieved,
            EPickingOrderStatus.Neu => AppResources.New,
            EPickingOrderStatus.Geloescht => AppResources.Deleted,
            EPickingOrderStatus.Gesperrt => AppResources.Locked,
            EPickingOrderStatus.Begonnen => AppResources.Started,
            EPickingOrderStatus.Fehlerhaft => AppResources.Incorrect,
            EPickingOrderStatus.Abgeschlossen => AppResources.Finish,
            EPickingOrderStatus.Unterbrochen => AppResources.Paused,
            _ => AppResources.Incorrect
        };

    public string GetDateOrder_yyyyMMdd()
    {
        DateTime dt = DateTime.ParseExact(Date, "dd.MM.yyyy", CultureInfo.InvariantCulture);
        return dt.ToString("yyyy.MM.dd");
    }
    public event PropertyChangedEventHandler PropertyChanged;
}
#endregion