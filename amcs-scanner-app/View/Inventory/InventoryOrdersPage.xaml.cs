using amcs_scanner_app.Client;
using amcs_scanner_app.Handler;
using amcs_scanner_app.Helper;
using amcs_scanner_app.Resources.Localization;
using amcs_shared_lib.Enum;
using amcs_shared_lib.Models.DTO;
using amcs_shared_lib.Models.HTTP;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Globalization;

namespace amcs_scanner_app.View.Inventory;

public partial class InventoryOrdersPage : ContentPage
{
    private readonly BarcodeParser _barcodeParser;
    private ObservableCollection<ResultInventoryOrder> Results { get; set; }
    private bool IsAsc { get; set; }
    private int Counter {get; set; } = 0;
    public bool IsRightPlaceSelectedBtn {get; set; }
    private int _maxSelected;
    private AppSettings _settings;
    private readonly InventoryClient _client;
    private HubConnection _ordersHubConnection;
    private readonly InventoryHandler _handler;
    private static App App => Application.Current as App;
    //public InventoryOrdersPage( AppSettings settings ) 
    public InventoryOrdersPage(InventoryClient client, AppSettings settings, InventoryHandler handler)
    {
		InitializeComponent();
        _client = client;
        _handler = handler;
        _barcodeParser = new BarcodeParser(settings);
        _settings = settings;
        _maxSelected = settings.MaxCombinedOrders;
        IsRightPlaceSelectedBtn = true;
        IsAsc = true;
        
        ListOrders.HeightRequest = _settings.DisplayHeight * 0.85;
        ListPickingOrders.MaximumHeightRequest = _settings.DisplayHeight * 0.7;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (App.OrderPageReload)
        {
            Results = new ObservableCollection<ResultInventoryOrder>();
            ListOrders.ItemsSource = Results;
            Setup();
            
        }
        App.ScannerScannedEvent += OnScannerScanned;
    }

    public ActivityIndicator SpinningStart()
    {
        Spinner.IsVisible = true;
        Spinner.IsRunning = true;
        ButtonsViewElement.IsVisible = false;

        return new ActivityIndicator { IsRunning = true, Color = Colors.Orange };
    }

    public ActivityIndicator SpinningEnd()
    {
        Spinner.IsVisible = false;
        Spinner.IsRunning = false;
        ButtonsViewElement.IsVisible = true;

        return new ActivityIndicator { IsRunning = false, Color = Colors.Orange };
    }

    public async void SetupData()
    {
        SpinningStart();
        List<InventoryOrderOverviewDTO> orders;
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
            .WithUrl(_settings.InventorySettings.BaseUrl + _settings.InventorySettings.InventoryOrderStatusHub, opt =>  
            {
                opt.Headers.Add("Authorization", "Bearer " + token);
            })
            .Build();

        _ordersHubConnection.On<string>("InventoryOrderStatusChange", (message) =>            
        {
            var statusChange = JsonSerializer.Deserialize<SetInventoryListRequest>(message); 

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

    public async Task<List<InventoryOrderOverviewDTO>> GetOrders()  
    {
        try
        {
            return await _client.GetInventoryListOverview();          
        }
        catch (Exception e)
        {
            return new List<InventoryOrderOverviewDTO>();
        }
    }
    private void CreateItems(List<InventoryOrderOverviewDTO> orderDTOs) 
    {
        Results.Clear();
        for (int i = 0; i < orderDTOs.Count; i++)
        {
            Results.Add(new ResultInventoryOrder(orderDTOs[i]));             
        }
        Results.ToList().RemoveAll(x => x.ItemCount < 1);
        RefreshItems(new ObservableCollection<ResultInventoryOrder>(Results.OrderByDescending(x => x.IsActive)));
        ListOrders.ItemsSource = Results;
    }
    public void UpdateStatus(SetInventoryListRequest change)               
    {
        var order = Results.FirstOrDefault(x => x.Id == change.Id);

        if (order != null) Results.FirstOrDefault(x => x.Id == change.Id).Status = (short)change.OrderStatus;
    }

    private async void Setup()
    {
        SpinningStart();

        await Task.Delay(1500);
        //Results.Clear();
        for (int i = 1; i < 30; i++)
            {
                DateTime startTime = new DateTime(2020 + new Random().Next(0, 3), new Random().Next(1, 13), new Random().Next(1, 28));
                DateTime endTime = new DateTime(startTime.Year + 1, new Random().Next(1, 13), new Random().Next(1, 28));
                var status = (short)new Random().Next(0, 7);
                if (status == 6) status = 9;
            
                var itemCount = new Random().Next(2, 30);

                Results.Add(new ResultInventoryOrder(new InventoryOrderOverviewDTO() { Id = i * 100000, ItemCount = itemCount, StartDate = startTime, EndDate = endTime ,Status = status })); ;
            }
            SortStartDate();
        
        SpinningEnd();
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

            if (codesScanned.Count() == 1)
            {
                var code = codesScanned[0];
                if (!(code.Identifier == _settings.ApplicationIdentifiers.Order.Identifier))
                    throw new InvalidOperationException(AppResources.ScannedCodeWasNoOrder);

                if (!Results.Any(x => x.Id.ToString() == code.Value && x.IsActive))
                    throw new InvalidOperationException(AppResources.ScannedOrderNotInList);

                ResultInventoryOrder result = Results.First(x => x.Id.ToString() == code.Value && x.IsActive);
                CheckItem(result);
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

    private void RefreshItems(ObservableCollection<ResultInventoryOrder> tempResults)
    {
        for (int i = 0; i < tempResults.Count; i++)
        {
            tempResults[i].Position = i + 1;
        }
        Results = new ObservableCollection<ResultInventoryOrder>(tempResults);
        ListOrders.ItemsSource = Results;
    }

    private async void ListOrders_ItemTapped(object sender, EventArgs e)
    {
        if (!IsRightPlaceSelectedBtn) return;
        if (Counter < 0)
        {
            //hinzuf�gen eine Exception
            await DisplayAlert("Fehler", "Counter: " + Counter, "Ok");
            return;
        }

        var viewCell = sender as ViewCell;
        ResultInventoryOrder result = Results[int.Parse(viewCell.ClassId) - 1];
        CheckItem(result);

    }

    private async void CheckItem(ResultInventoryOrder result)
    { 
        if (result == null || !result.IsActive) return;

        if (Counter == _maxSelected && !result.IsSelectedItem)
        {
            await DisplayAlert("Sie k�nnen nur " + _maxSelected + " Auftr�ge ausw�hlen", " ", "Ok");
            return;
        }

        if (_maxSelected == 1 && !result.IsSelectedItem)
        {
            var orders = new List<ResultInventoryOrder> { result };
            // order als begonnen melden
            /*try
            {
                await _client.PostSetInventoryList(new SetInventoryListRequest
                {
                    Id = result.Id,
                    OrderStatus = amcs_shared_lib.Enum.EInventoryOrderStatus.Begonnen
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
            }*/
            App.OrderItemsPageReload = true;
            App.InventoryOrders = orders;
            await Shell.Current.GoToAsync(nameof(InventoryItemsPage));
            return;
        }
        ShowSelectedBtn(result);
    }


    private async void ShowSelectedBtn(ResultInventoryOrder result)
    {
        uint speedUp = 150;
        var moveY = _settings.DisplayHeight / 3;
        if (result.IsSelectedItem)
        {
            Counter--;

            if (Counter < 1)
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
            Counter++;

            if (Counter > 0)
            {
                ShowSelectedOrdersBtn.IsVisible = true;
                IsRightPlaceSelectedBtn = false;
                await ShowSelectedOrdersBtn.TranslateTo(0, -moveY, speedUp, Easing.Linear);
                IsRightPlaceSelectedBtn = true;
            }
            result.IsSelectedItem = true;
        }
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
        ButtonFlyoutOpen.IsEnabled = false;
    }

    public List<ResultInventoryOrder> GetSelectedOrders() =>
        Results.Where(x => x.IsSelectedItem).ToList();

    private void CloseSelectedOrder(object sender, EventArgs e)
    {
        ViewSelectedOrders.IsVisible = false;
        RefreshItems(Results);
        ListOrders.IsVisible = true;
        ButtonsViewElement.IsVisible = true;
        ShowSelectedOrdersBtn.IsVisible = true;
        ButtonFlyoutOpen.IsEnabled = true;
    }

    private async void Start(object sender, EventArgs e)
    {
        StartBtn.IsEnabled = false;
        StartBtn.IsVisible = false;
        
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

                App.OrderItemsPageReload = true;
                App.InventoryOrders = orders;
                await Shell.Current.GoToAsync(nameof(InventoryItemsPage));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
        }
        
    }

    private async Task<List<ResultInventoryOrder>> SetOrderStatusStarted()
    {
        var orders = GetSelectedOrders();

        foreach (var item in orders)
        {
            item.IsSelectedItem = false;
            Counter--;
            item.Status = (short)EInventoryOrderStatus.Begonnen;//item.StatusName = AppResources.Started;

            /*var response = await _client.PostSetInventoryList(new SetInventoryListRequest
            {
                Id = item.Id,
                OrderStatus = EInventoryOrderStatus.Begonnen
            });

            if (response is null)
            {
                throw new InvalidOperationException(AppResources.NoConnectionToSystem);
            }*/
        }
        return orders;
    }

    private void ShowWebView(object sender, EventArgs e)
    {
        ListOrders.IsEnabled = false;
        ShowSelectedOrdersBtn.IsEnabled = false;
        SortEndDateBtn.IsEnabled = false;
        SortItemCountBtn.IsEnabled = false;
        SortStartDateBtn.IsEnabled = false;
        SortEndDateImage.IsEnabled = false;
        SortItemCountImage.IsEnabled = false;
        SortStartDateImage.IsEnabled = false;


        var button = sender as ImageButton;
        int index = int.Parse(button.ClassId) - 1;
        var text1 = Results[index].Id;
        var text2 = Results[index].StartDate;

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
        SortEndDateBtn.IsEnabled = true;
        SortItemCountBtn.IsEnabled = true;
        SortStartDateBtn.IsEnabled = true;
        SortEndDateImage.IsEnabled = true;
        SortItemCountImage.IsEnabled = true;
        SortStartDateImage.IsEnabled = true;
    }
    private void SortItemCount(object sender, EventArgs e)
    {
        if (IsAsc) RefreshItems(new ObservableCollection<ResultInventoryOrder>(Results.OrderBy(x => x.ItemCount).OrderByDescending(x => x.IsActive)));
        else RefreshItems(new ObservableCollection<ResultInventoryOrder>(Results.OrderByDescending(x => x.ItemCount).OrderByDescending(x => x.IsActive)));
        SortStartDateImage.IsVisible = false;
        SortEndDateImage.IsVisible = false;
        SortItemCountImage.IsVisible = true;
        OnArrow(IsAsc, SortItemCountImage);
        IsAsc = !IsAsc;
    }
    private void SortEndDate(object sender, EventArgs e)
    {
        if (IsAsc) RefreshItems(new ObservableCollection<ResultInventoryOrder>(Results.OrderBy(x => x.GetDateOrder_yyyyMMdd(x.EndDate)).OrderByDescending(x => x.IsActive)));
        else RefreshItems(new ObservableCollection<ResultInventoryOrder>(Results.OrderByDescending(x => x.GetDateOrder_yyyyMMdd(x.EndDate)).OrderByDescending(x => x.IsActive)));
        SortStartDateImage.IsVisible = false;
        SortEndDateImage.IsVisible = true;
        SortItemCountImage.IsVisible = false;
        OnArrow(IsAsc, SortEndDateImage);
        IsAsc = !IsAsc;
    }
    private void SortStartDate(object sender, EventArgs e)
    {
        SortStartDate();
    }
    private void SortStartDate()
    {
        if (IsAsc) RefreshItems(new ObservableCollection<ResultInventoryOrder>(Results.OrderBy(x => x.GetDateOrder_yyyyMMdd(x.StartDate)).OrderByDescending(x => x.IsActive)));
        else RefreshItems(new ObservableCollection<ResultInventoryOrder>(Results.OrderByDescending(x => x.GetDateOrder_yyyyMMdd(x.StartDate)).OrderByDescending(x => x.IsActive)));
        SortStartDateImage.IsVisible = true;
        SortEndDateImage.IsVisible = false;
        SortItemCountImage.IsVisible = false;
        OnArrow(IsAsc, SortStartDateImage);
        IsAsc = !IsAsc;
    }
    private void OnArrow(bool isAsc, ImageButton image)
    {
        if (isAsc)
        {
            if (App.Current.UserAppTheme == AppTheme.Light) image.Source = "arrowup_light.png";
            else image.Source = "arrowup_dark.png";
        }
        else
        {
            if (App.Current.UserAppTheme == AppTheme.Light) image.Source = "arrowdown_light.png";
            else image.Source = "arrowdown_dark.png";
        }
    }

    private void FlyoutOpen(object sender, EventArgs e)
    {
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Flyout;
        Shell.Current.FlyoutIsPresented = true;
    }
}

public class ResultInventoryOrder : INotifyPropertyChanged
{
    public long Id { get; private set; }
    public string StatusName { get; private set; }
    public string StartDate { get; private set; }
    public string EndDate { get; private set; }
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
                    ColorStatus = App.GetColorResource("StatusNeu");//Neu
                    //StatusName = AppResources.New;
                    IsActive = false;
                    break;
                case 1:
                    ColorStatus = App.GetColorResource("StatusReceived");//Abgeholt
                    //StatusName = AppResources.New;
                    IsActive = true;
                    break;
                case 2:
                    ColorStatus = App.GetColorResource("StatusStarted");//Begonnen
                    //StatusName = AppResources.Started;
                    IsActive = false;
                    break;
                case 3:
                    ColorStatus = App.GetColorResource("StatusInterrupted");//Unterbrochen
                    //StatusName = AppResources.Paused;
                    IsActive = true;
                    break;
                case 4:
                    ColorStatus = App.GetColorResource("StatusFinish");//Abgeschlossen
                    //StatusName = AppResources.Finish;
                    IsActive = false;
                    break;
                case 5:
                    ColorStatus = App.GetColorResource("StatusDeleted");//Gel�scht
                    //StatusName = AppResources.Deleted;
                    IsActive = false;
                    break;
                case 9:
                    ColorStatus = App.GetColorResource("StatusIncorrect");//Fehlerhaft
                    //StatusName = AppResources.Incorrect;
                    IsActive = false;
                    break;
                default:
                    ColorStatus = App.GetColorResource("StatusIncorrect");
                    IsActive = false;
                    break;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorStatus)));
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusName)));
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
            else Opacitylevel = 0.5;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Opacitylevel)));
        }
    }

    //public long PickingOrderId { get; private set; }
    //public string ItemId { get; private set; }
    public int ItemCount { get; private set; }

    private bool isSelectedItem;
    public bool IsSelectedItem
    {
        get => isSelectedItem;
        set
        {
            //if (!isActive) return;

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

    public List<InventoryItemDTO> InventoryItems { get; }

    public ResultInventoryOrder(InventoryOrderOverviewDTO inventoryOrderDTO)
    {
        StartDate = String.Format("{0:dd.MM.yyyy}", inventoryOrderDTO.StartDate);
        EndDate = String.Format("{0:dd.MM.yyyy}", inventoryOrderDTO.EndDate);
        Id = (long)inventoryOrderDTO.Id;
        Status = inventoryOrderDTO.Status;
        StatusName = GetStatusName((EInventoryOrderStatus)Status);
        ItemCount = inventoryOrderDTO.ItemCount;
        IsSelectedItem = false;
        //InventoryItems = inventoryOrderDTO.InventoryItems;
    }

    public string GetStatusName(EInventoryOrderStatus status) =>
        status switch
        {
            EInventoryOrderStatus.Abgeholt => AppResources.Retrieved,
            EInventoryOrderStatus.Neu => AppResources.New,
            EInventoryOrderStatus.Gelöscht => AppResources.Deleted,
            EInventoryOrderStatus.Gesperrt => AppResources.Locked,
            EInventoryOrderStatus.Begonnen => AppResources.Started,
            EInventoryOrderStatus.Fehlerhaft => AppResources.Incorrect,
            EInventoryOrderStatus.Abgeschlossen => AppResources.Finish,
            EInventoryOrderStatus.Unterbrochen => AppResources.Paused,
            _ => AppResources.Incorrect
        };

    public string GetDateOrder_yyyyMMdd(string Date)
    {
        DateTime dt = DateTime.ParseExact(Date, "dd.MM.yyyy", CultureInfo.InvariantCulture);
        return dt.ToString("yyyy.MM.dd");
    }
    public event PropertyChangedEventHandler PropertyChanged;
}