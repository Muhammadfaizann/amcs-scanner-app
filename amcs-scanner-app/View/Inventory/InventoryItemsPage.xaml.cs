using amcs_scanner_app.Client;
using amcs_scanner_app.Handler;
using amcs_scanner_app.Helper;
using amcs_scanner_app.Resources.Localization;
using amcs_shared_lib.Models.DTO;
using amcs_shared_lib.Models.HTTP;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;

namespace amcs_scanner_app.View.Inventory;

public partial class InventoryItemsPage : ContentPage
{
    private ObservableCollection<ResultInventoryItem> ResultItems { get; set; } = new ObservableCollection<ResultInventoryItem>();
    private InventoryClient _client;
    private readonly AppSettings _settings;
    private readonly InventoryHandler _handler;
    private readonly BarcodeParser _barcodeParser;

    private string CurrentUserId;
    private string CurrentUserName;
    private bool IsShowMessage { get; set; }
    private List<long> currentOrderIds { get; set; }
    private static App app => Application.Current as App;
    private ResultInventoryItem _resultItem;

    private ObservableCollection<ResultItemStoragePlace> storagePlaces;

    private long selectedId;
    private decimal selectedAmount;
    private string selectedStorage;
    private int countPicking = 0;

    public InventoryItemsPage(InventoryClient client, AppSettings settings, InventoryHandler handler)
    {
        InitializeComponent();

        _handler = handler;
        _client = client;
        _settings = settings;
        _barcodeParser = new BarcodeParser(settings);

        handler.ItemInventoriedEvent += OnHandlerUpdateItem;

        ListItems.HeightRequest = _settings.DisplayHeight * 0.85;
        PauseBtn.WidthRequest = _settings.DisplayWidth * 0.4;
        FinishBtn.WidthRequest = _settings.DisplayWidth * 0.4;

    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Spinner.IsVisible = false;

        IsListItemsEnabled(true);
        app.ScannerScannedEvent += OnScannerScanned;
        if (App.OrderItemsPageReload)
        {
            currentOrderIds = App.Orders.Select(x => x.Id).ToList();
            ResultItems = new ObservableCollection<ResultInventoryItem>();
            Setup(App.InventoryOrders);
            //ResultItems = new ObservableCollection<ResultInventoryItem>(ResultItems.OrderBy(x => x.Id).OrderBy(x => x.ItemId).OrderByDescending(x => x.IsActive));
            ListItems.ItemsSource = ResultItems;
        }
    }

    #region LoadingSpinner
    public ActivityIndicator SpinningStart()
    {
        Spinner.IsVisible = true;
        Spinner.IsRunning = true;
        ViewButtonsBottom.IsVisible = false;

        return new ActivityIndicator { IsRunning = true, Color = Colors.Orange };
    }

    public ActivityIndicator SpinningEnd()
    {
        Spinner.IsVisible = false;
        Spinner.IsRunning = false;
        ViewButtonsBottom.IsVisible = true;

        return new ActivityIndicator { IsRunning = false, Color = Colors.Orange };
    }
    #endregion

    #region Setup und Eventhandler

    public async void Setup(List<ResultInventoryOrder> orders)
    {
        SpinningStart();
        await Task.Delay(1000);
        /*try
        {
            CurrentUserId = await SecureStorage.GetAsync("UserId");
            CurrentUserName = await SecureStorage.GetAsync("UserName");
            List<KeyValuePair<long, int>> orderIndex = new List<KeyValuePair<long, int>>();
            var items = new List<InventoryItemDTO>();
            for (int i = 0; i < orders.Count; i++)
            {
                var x = orders[i];
                var response = await _client.GetInventoryListById(x.Id);
                if (!response.Success)
                    throw new InvalidOperationException(response.ErrorMessage);

                var order = response.PickingOrders.SingleOrDefault(); // !!! Korrigieren in amcs_shared_lib.Models.HTTP.GetInventoryListResponse: PickingOrders --> InventoryOrders
                if (order is null)
                    throw new Exception("No Items in Response");

                items.AddRange(order.InventoryItems);
                orderIndex.Add(new KeyValuePair<long, int>(order.Id, i));
            }

            
            List<ResultInventoryItem> results = new List<ResultInventoryItem>();
            // Nur sortieren wenn alle Items Sort-Kriterium haben
            if (!items.Any(x => x.Sort is null)) items.Sort((x, y) => BigInteger.Compare(long.Parse(x.Sort), long.Parse(y.Sort)));
            
            items.ForEach(i => results.Add(new ResultInventoryItem(i, orderIndex.Where(x => x.Key == i.Id).First().Value)));
            results.ForEach(x => ResultItems.Add(x));
        }
        catch (Exception e)
        {
            // Error loading Picklists ... Handle here
        }
        finally
        {
            SpinningEnd();
            
        }*/

        for (int i = 0; i < orders.Count; i++)
        {
            for (int j = 1; j < orders[i].ItemCount + 1; j++)
            {
                string amountUnit;

                if (new Random().Next(0, 2) == 1) amountUnit = "Stk.";
                else amountUnit = "Pck.";

                string itemId;
                if (j < 10) itemId = "0" + (j * 10000);
                else itemId = "" + (j * 10000);

                var storagePlacesCount = new Random().Next(2, 3);
                List<ItemStoragePlaceDTO> storagePlaces = new List<ItemStoragePlaceDTO>();

                for (int k = 1; k < storagePlacesCount; k++)
                {
                    storagePlaces.Add(new ItemStoragePlaceDTO() { Id = (i * 10000) + (j * 100) + k, PickItemId = long.Parse(SecureStorage.GetAsync("UserId").Result), StorageId = "" + (j * 10000 + k) });
                }

                ResultItems.Add(new ResultInventoryItem(new InventoryItemDTO() { Id = orders[i].Id, ItemId = itemId, ExpectedAmount = new Random().Next(1, 990), ItemName = "" + j + ". " + i + " ArtikelName", AmountUnit = amountUnit, Status = (short)new Random().Next(0, 4), ItemStoragePlaces = storagePlaces }, i));
            }
        }

        ResultItems = new ObservableCollection<ResultInventoryItem>(ResultItems.OrderBy(x => x.Id).OrderBy(x => x.ItemId).OrderByDescending(x => x.IsActive));

        if (_settings.IsSequentialPicking) UpdateSequence();
        else
            for (int i = 0; i < ResultItems.Count; i++)
            {
                ResultItems[i].IsSelectable = true;
                ResultItems[i].Position = i + 1;
            }
        ListItems.ItemsSource = ResultItems;
        SpinningEnd();
    }

    public void OnHandlerUpdateItem(object sender, ItemInventoriedEventArgs e)
    {
        var item = ResultItems.FirstOrDefault(x => x.Id == e.Id);
        if (item is null) return;
        item.Status = 2;
    }

    private async void OnScannerScanned(object sender, ScannerEventArgs e)
    {
        IsListItemsEnabled(false);
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
            if (!(code.Identifier == _settings.ApplicationIdentifiers.Article.Identifier))
                throw new InvalidOperationException("No Article!");

            if (!ResultItems.Any(x => x.ItemId.ToString() == code.Value))
                throw new InvalidOperationException(AppResources.ScannedOrderNotInList);


            _resultItem = ResultItems.First(x => x.ItemId.ToString() == code.Value);

            ItemInventory();
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
            IsListItemsEnabled(true);
        }
    }

    #endregion

    #region Öffnen/Schließen der Position
    /// <summary>
    /// Funktionen zum öffnen und schließen einer Position
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ListItems_Tapped(object sender, EventArgs e)
    {
        IsListItemsEnabled(false);
        var viewCell = sender as ViewCell;
        // !!! viewCell.ClassId - Position
        ResultInventoryItem resultItem = ResultItems[int.Parse(viewCell.ClassId) - 1];
        if (resultItem == null)
        {
            IsListItemsEnabled(true);
            return; //resultItem.IsSelectable
        }
        _resultItem = resultItem;
        ItemInventory();
    }

    private async void ItemInventory()
    {
        if (!_resultItem.IsActive || !_resultItem.IsSelectable)
        {
            IsListItemsEnabled(true);
            return;
        }

        App.ResultInventoryItem = _resultItem;
        await Shell.Current.GoToAsync(nameof(ShowItem));

        app.ScannerScannedEvent -= OnScannerScanned;
    }

    private async void ShowMissing(object sender, EventArgs e)
    {
        app.ScannerScannedEvent -= OnScannerScanned;
        _resultItem = new ResultInventoryItem(new InventoryItemDTO(), 12);
        App.ResultInventoryItem = _resultItem;
        await Shell.Current.GoToAsync(nameof(ShowMissingItem));
    }

    private void CloseItem()
    {
        //_resultItem.IsSelectedItem = false;
        ListItems.SelectedItem = null;
        if (_settings.IsSequentialPicking) UpdateSequence();
        IsListItemsEnabled(true);
    }

    private void UpdateSequence()
    {
        int counter = 0;
        for (int i = 0; i < ResultItems.Count; i++)
        {
            if (ResultItems[i].IsActive) counter++;

            if (counter != countPicking + 1) ResultItems[i].IsSelectable = false;
            else ResultItems[i].IsSelectable = true;

            //ResultItems[i].IsSelectedItem = false;
            ResultItems[i].Position = i + 1;
        }
    }

    #endregion

    #region Pause/Abschluss
    
    private async void Pause(object sender, EventArgs e)
    {
        /*IsListItemsEnabled(false);

        if (await DisplayAlert(AppResources.Park, AppResources.PausePicking, AppResources.Ok, AppResources.Cancel))  // Missing AppResources.PauseInventory
        {
            SpinningStart();
            try
            {
                foreach (var currentOrderId in currentOrderIds)
                {
                    var response = await _client.PostSetInventoryList(new SetInventoryListRequest { Id = currentOrderId, OrderStatus = amcs_shared_lib.Enum.EInventoryOrderStatus.Unterbrochen });
                    if (response.Success)
                    {
                        SpinningEnd();
                        app.ScannerScannedEvent -= OnScannerScanned;
                        App.OrderPageReload = false;
                        await Shell.Current.GoToAsync(nameof(InventoryOrdersPage));
                    }
                    else
                    {
                        IsListItemsEnabled(true);
                        throw new SystemException(response.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                SpinningEnd();
                IsListItemsEnabled(true);
                await DisplayAlert(AppResources.Error, AppResources.NoConnectionToSystem, AppResources.Ok);
            }
        }
        else IsListItemsEnabled(true);*/
        
        App.OrderPageReload = false;
        await Shell.Current.GoToAsync(nameof(InventoryOrdersPage));

    }

    private async void Finish(object sender, EventArgs e)
    {
        /*IsListItemsEnabled(false);

        if (await DisplayAlert(AppResources.End, AppResources.EndCommission, AppResources.Ok, AppResources.Cancel))  // Missing AppResources.EndInventory
        {
            if (ResultItems.Any(x => x.Status == 3))
            {
                await DisplayAlert(AppResources.Attention, AppResources.StillGotCachedItems, AppResources.Ok);
                IsListItemsEnabled(true);
                return;
            }

            if (ResultItems.Any(x => x.Status == 0))
            {
                // Inventur darf nur beendet werden, wenn AppSettings.AllInventoryItemsMustBeDone = false
                switch (_settings.AllInventoryItemsMustBeDone)
                {
                    case false:
                        if (!await DisplayAlert(AppResources.Attention, AppResources.NotAllPositionsDone, AppResources.Ok, AppResources.Cancel))
                        {
                            IsListItemsEnabled(true);
                            return;
                        }
                        break;
                    default:
                        await DisplayAlert(AppResources.Attention, AppResources.NotAllPositionsDone, AppResources.Ok);
                        IsListItemsEnabled(true);
                        return;
                }
            }

            try
            {
                SpinningStart();
                foreach (var currentOrderId in currentOrderIds)
                {
                    _handler.OrderFinished(new SetInventoryListRequest { Id = currentOrderId, OrderStatus = amcs_shared_lib.Enum.EInventoryOrderStatus.Abgeschlossen });
                }
                SpinningEnd();
                app.ScannerScannedEvent -= OnScannerScanned;
                await Shell.Current.Navigation.PushAsync(new WaitingForConnection(_handler));
            }
            catch (Exception ex)
            {
                await DisplayAlert(AppResources.Error, AppResources.NoConnectionToSystem, AppResources.Ok);
                IsListItemsEnabled(true);
            }
        }
        else
        {
            IsListItemsEnabled(true);
        }*/
        
        App.OrderPageReload = false;
        await Shell.Current.GoToAsync(nameof(InventoryOrdersPage));
    }

    private void IsListItemsEnabled(bool isEnabled)
    {
        ListItems.IsEnabled = isEnabled;
        PauseBtn.IsEnabled = isEnabled;
        FinishBtn.IsEnabled = isEnabled;
    }

    public void Send(ObservableCollection<ResultItemStoragePlace> placesFromModal, long itemId)
    {
        /*var content = new List<SetInventoryStoragePlaceRequest>();
        placesFromModal.ToList().ForEach(sp =>
        {
            var x = long.Parse(sp.StorageId);
            content.Add(new SetInventoryStoragePlaceRequest { ActualAmount = TestDecimal(sp.PlaceValue), Barcode = sp.BatchNumber, StoragePlace = sp.StorageName, Id = x, UserId = CurrentUserId });
        });

        // Statuszahl für Cached.(Nur in App)
        ResultItems.FirstOrDefault(x => x.Id == itemId).Status = 99;
        
        _handler.ItemPicked(new SetInventoryItemRequest { Id = itemId,*/ /*PickerID = long.Parse(CurrentUserId),*/ /*Status = amcs_shared_lib.Enum.EInventoryItemStatus.Gezaehlt, UserId = CurrentUserId, ItemStoragePlaceList = content });

        if (_settings.IsSequentialPicking) countPicking++;

        CloseItem();

        if (_settings.IsSequentialPicking && _resultItem.Position < ResultItems.Count)
        {
            _resultItem = ResultItems[_resultItem.Position];
            //_resultItem.IsSelectedItem = true;
            if (!_resultItem.IsActive)
            {
                DisplayAlert("Warnung", "Dieser Artikel ist der letzte aktive", "Ok");
                return;
            }

            App.ResultInventoryItem = _resultItem;
            Shell.Current.GoToAsync(nameof(ShowItem));

        }*/

        CloseItem();
    }

    public decimal TestDecimal(string value)
    {
        if (value == null || value == "") return 0;

        if (decimal.TryParse(value, out decimal number))
        {
            if (number >= 0)
                return number;
        }
        return -1;
    }

    #endregion

}

public class ResultInventoryItem : INotifyPropertyChanged
{

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
                    ColorStatus = App.GetColorResource("StatusNew");
                    IsActive = true;
                    break;
                case 1:
                    ColorStatus = App.GetColorResource("StatusReceived");
                    IsActive = false;
                    break;
                case 2:
                    ColorStatus = App.GetColorResource("StatusStarted");
                    IsActive = true;
                    break;
                case 3:
                    ColorStatus = App.GetColorResource("StatusInterrupted");
                    IsActive = true;
                    break;
                case 4:
                    ColorStatus = App.GetColorResource("StatusFinish");
                    IsActive = false;
                    break;
                case 5:
                    ColorStatus = App.GetColorResource("StatusDeleted");
                    IsActive = false;
                    break;
                case 9:
                    ColorStatus = App.GetColorResource("StatusIncorrect");
                    IsActive = false;
                    break;
                case 99:
                    ColorStatus = App.GetColorResource("StatusCache");
                    break;
                default:
                    ColorStatus = App.GetColorResource("StatusIncorrect");
                    IsActive = false;
                    break;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorStatus)));
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

    private bool isSelectable;
    public bool IsSelectable
    {
        get => isSelectable;
        set
        {
            isSelectable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectable)));
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

    public long Id { get; set; }
    public string ItemId { get; private set; }
    public List<ItemStoragePlaceDTO> ItemStoragePlaces { get; set; }
    public long SelectedStoragePlaceId { get; set; }
    public string AmountUnit { get; private set; }
    public decimal? ExpectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public string ItemName { get; private set; }
    public string UnitName { get; private set; }

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

    public Color OrderColor { get; set; }

    public ResultInventoryItem(InventoryItemDTO inventoryItemDTO, int index)
    {
        Id = inventoryItemDTO.Id;
        ItemId = inventoryItemDTO.ItemId;
        AmountUnit = inventoryItemDTO.AmountUnit is null ? "" : inventoryItemDTO.AmountUnit;
        ExpectedAmount = inventoryItemDTO.ExpectedAmount is null ? 1 : Math.Floor(inventoryItemDTO.ExpectedAmount.Value);
        ItemName = inventoryItemDTO.ItemName is null ? "No Name" : inventoryItemDTO.ItemName;
        IsActive = true;
        ColorTextItem = App.GetColorResource("Black");
        Status = inventoryItemDTO.Status;
        ItemStoragePlaces = inventoryItemDTO.ItemStoragePlaces.Any() ? inventoryItemDTO.ItemStoragePlaces : new List<ItemStoragePlaceDTO>() { new ItemStoragePlaceDTO
        {
            StorageId = inventoryItemDTO.ItemName,
            StoragePlace = inventoryItemDTO.ItemName,
            PickItemId = inventoryItemDTO.Id
        } };
        OrderColor = GetOrderColor(index);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private Color GetOrderColor(int index) =>
        index switch
        {
            0 => App.GetColorResource("OrderOne"),
            1 => App.GetColorResource("OrderTwo"),
            2 => App.GetColorResource("OrderThree"),
            3 => App.GetColorResource("OrderFour"),
            4 => App.GetColorResource("OrderFive"),
            5 => App.GetColorResource("OrderSix"),
            6 => App.GetColorResource("OrderSeven"),
            7 => App.GetColorResource("OrderEight"),
            8 => App.GetColorResource("OrderNine"),
            9 => App.GetColorResource("OrderTen"),
            _ => App.GetColorResource("White"),
        };
}