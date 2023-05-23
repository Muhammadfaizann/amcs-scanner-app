using amcs_scanner_app.Client;
using amcs_scanner_app.Helper;
using amcs_scanner_app.Handler;
using amcs_shared_lib.Models.DTO;
using amcs_shared_lib.Models.HTTP;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using amcs_scanner_app.Resources.Localization;
using amcs_shared_lib.Enum;
using Microsoft.Maui.Controls;

namespace amcs_scanner_app.View;
//[QueryProperty(nameof(Orders), "Orders")]
public partial class PickingOrderItemsPage : ContentPage
{
    private ObservableCollection<ResultItem> ResultItems { get; set; } = new ObservableCollection<ResultItem>();
    private WarehouseClient _warehouse;
    private readonly AppSettings _settings;
    private readonly PickingHandler _pickingHandler;
    private readonly BarcodeParser _barcodeParser;

    private string CurrentUserId;
    private string CurrentUserName;
    private bool IsShowMessage { get; set; }
    private List<long> currentOrderIds { get; set; }
    private static App app => Application.Current as App;
    private ResultItem _resultItem;

    private ObservableCollection<ResultItemStoragePlace> storagePlaces;

    private long selectedId;
    private decimal selectedAmount;
    private string selectedStorage;
    private int countPicking = 0;

    public PickingOrderItemsPage(WarehouseClient client, AppSettings settings, PickingHandler handler)
    {
        InitializeComponent();
        _pickingHandler = handler;
        _warehouse = client;
        _settings = settings;
        _barcodeParser = new BarcodeParser(settings);

        handler.ItemPickedEvent += OnHandlerUpdateItem;

        ListItems.HeightRequest = _settings.DisplayHeight * 0.85;
        PauseBtn.WidthRequest = _settings.DisplayWidth * 0.4;
        FinishBtn.WidthRequest = _settings.DisplayWidth * 0.4;

    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        IsListItemsEnabled(true);
        app.ScannerScannedEvent += OnScannerScanned;
        if(App.OrderItemsPageReload)
        {
            currentOrderIds = App.Orders.Select(x => x.Id).ToList();
            ResultItems = new ObservableCollection<ResultItem>();
            Setup(App.Orders);
            ResultItems = new ObservableCollection<ResultItem>(ResultItems.OrderBy(x => x.PickingOrderId).OrderBy(x => x.ItemId).OrderByDescending(x => x.IsActive));
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
    private void FlyoutOpen(object sender, EventArgs e)
    {
        Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
    }
    #endregion

    #region Setup und Eventhandler
    /// <summary>
    /// Setup-Methode baut die Liste der Positionen
    /// </summary>
    /// <param name="orders"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public async void Setup(List<ResultOrder> orders)
    {
        SpinningStart();
        try
        {
            CurrentUserId = await SecureStorage.GetAsync("UserId");
            CurrentUserName = await SecureStorage.GetAsync("UserName");
            List<KeyValuePair<long, int>> orderIndex = new List<KeyValuePair<long, int>>();
            var allitems = new List<PickingOrderItemDTO>();
            for (int i = 0; i < orders.Count; i++)
            {
                var x = orders[i];
                var response = await _warehouse.GetPicklistById(x.Id);
                if (!response.Success)
                    throw new InvalidOperationException(response.ErrorMessage);

                var order = response.PickingOrders.SingleOrDefault();
                if (order is null)
                    throw new Exception("No Items in Response");

                allitems.AddRange(order.PickingOrderItems);
                orderIndex.Add(new KeyValuePair<long, int>(order.Id, i));
            }

            // Nur sortieren wenn alle Items Sort-Kriterium haben
            if (!allitems.Any(x => x.Sort is null))
            {
                var normalItems = allitems.Where(x => !x.Empties).ToList();
                var empties = allitems.Where(x => x.Empties).ToList();
                List<ResultItem> results = new List<ResultItem>();
                normalItems.Sort((x, y) => BigInteger.Compare(long.Parse(x.Sort), long.Parse(y.Sort)));
                empties.Sort((x, y) => BigInteger.Compare(long.Parse(x.Sort), long.Parse(y.Sort)));

                normalItems.ForEach(i => results.Add(new ResultItem(i, orderIndex.Where(x => x.Key == i.PickingId).First().Value)));
                // Leergut hinten anhängen
                empties.ForEach(i => results.Add(new ResultItem(i, orderIndex.Where(x => x.Key == i.PickingId).First().Value)));
                results.ForEach(x => ResultItems.Add(x));
            }
            else
            {
                var normalItems = allitems.Where(x => !x.Empties).ToList();
                var empties = allitems.Where(x => x.Empties).ToList();
                List<ResultItem> results = new List<ResultItem>();

                normalItems.ForEach(i => results.Add(new ResultItem(i, orderIndex.Where(x => x.Key == i.PickingId).First().Value)));
                empties.ForEach(i => results.Add(new ResultItem(i, orderIndex.Where(x => x.Key == i.PickingId).First().Value)));

                results.ForEach(x => ResultItems.Add(x));
            }
        }
        catch (Exception e)
        {
            // Error loading Picklists ... Handle here
        }
        finally
        {
            SpinningEnd();
            if (_settings.IsSequentialPicking) UpdateSequence();
            else
                for (int i = 0; i < ResultItems.Count; i++)
                {
                    ResultItems[i].IsSelectable = true;
                    ResultItems[i].Position = i + 1;
                }
        }
    }

    /// <summary>
    /// Updatemethode für Event, was vom Pickinghandler bei erfolgreicher Meldung ausgelöst wird.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public void OnHandlerUpdateItem(object sender, ItemPickedEventArgs e)
    {
        var item = ResultItems.FirstOrDefault(x => x.Id == e.Id);
        if (item is null)
            return;

        item.Status = 2;
    }

    /// <summary>
    /// Scanner in dieser View für Article-Barcodes oder Storageplace-Barcodes.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="InvalidOperationException"></exception>
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

            ItemPicking();
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
        ResultItem resultItem = ResultItems[int.Parse(viewCell.ClassId) - 1];
        if (resultItem == null)
        {
            IsListItemsEnabled(true);
            return;
        }
        _resultItem = resultItem;
        ItemPicking();
    }

    private void ItemPicking()
    {
        if (!_resultItem.IsActive || !_resultItem.IsSelectable)
        {
            IsListItemsEnabled(true);
            return;
        }
        if (_resultItem.Weighing == 0)
        {
            ShowListItemsPiece(_resultItem);
        }
        else
        {
            ShowListItemsWiege(_resultItem);
        }
        app.ScannerScannedEvent -= OnScannerScanned;
    }

    private async void ShowListItemsWiege(ResultItem resultItem)
    {
        App.ResultItem = resultItem;
        await Shell.Current.GoToAsync(nameof(ShowItemWiege));
    }

    private async void ShowListItemsPiece(ResultItem resultItem)
    {
        App.ResultItem = resultItem;
        await Shell.Current.GoToAsync(nameof(ShowItemPiece));
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

            ResultItems[i].Position = i + 1;
        }
    }

    #endregion

    #region Pause/Abschluss
    /// <summary>
    /// Methode zum Parken/Pausieren der Kommissionierung.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="SystemException"></exception>
    private async void Pause(object sender, EventArgs e)
    {
        IsListItemsEnabled(false);

        if (await DisplayAlert(AppResources.Park, AppResources.PausePicking, AppResources.Ok, AppResources.Cancel))
        {
            SpinningStart();
            try
            {
                foreach (var currentOrderId in currentOrderIds)
                {
                    var response = await _warehouse.PostSetPicklist(new SetPicklistRequest { Id = currentOrderId, OrderStatus = amcs_shared_lib.Enum.EPickingOrderStatus.Unterbrochen });
                    if (response.Success)
                    {
                        SpinningEnd();
                        app.ScannerScannedEvent -= OnScannerScanned;
                        App.OrderPageReload = false;
                        await Shell.Current.GoToAsync(nameof(PickingOrderPage));
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
        else IsListItemsEnabled(true);
    }

    /// <summary>
    /// Beenden der Kommissionierung
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="SystemException"></exception>
    private async void Finish(object sender, EventArgs e)
    {
        IsListItemsEnabled(false);

        if (await DisplayAlert(AppResources.End, AppResources.EndPicking, AppResources.Ok, AppResources.Cancel))
        {
            if (ResultItems.Any(x => x.Status == 3))
            {
                await DisplayAlert(AppResources.Attention, AppResources.StillGotCachedItems, AppResources.Ok);
                IsListItemsEnabled(true);
                return;
            }

            if (ResultItems.Any(x => x.Status == 0))
            {
                switch (_settings.AllPickingItemsMustBeDone)
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
                    _pickingHandler.OrderFinished(new SetPicklistRequest { Id = currentOrderId, OrderStatus = amcs_shared_lib.Enum.EPickingOrderStatus.Abgeschlossen });
                }
                SpinningEnd();
                app.ScannerScannedEvent -= OnScannerScanned;
                await Shell.Current.Navigation.PushAsync(new WaitingForConnection(_pickingHandler));
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
        }
    }

    private void IsListItemsEnabled(bool isEnabled)
    {
        ListItems.IsEnabled = isEnabled;
        PauseBtn.IsEnabled = isEnabled;
        FinishBtn.IsEnabled = isEnabled;
    }

    public void Send(ObservableCollection<ResultItemStoragePlace> placesFromModal, long itemId)
    {
        var content = new List<SetPickItemStoragePlaceRequest>();
        if (_resultItem.Weighing == 0)
        {
            placesFromModal.ToList().ForEach(sp =>
            {
                var x = long.Parse(sp.StorageId);
                content.Add(new SetPickItemStoragePlaceRequest { Amount = TestDecimal(sp.PlaceValue), Barcode = sp.BatchNumber, StoragePlace = sp.StorageName, Id = x, UserId = CurrentUserId });
            });
        }
        else
        {
            placesFromModal.ToList().ForEach(sp =>
            {
                var x = long.Parse(sp.StorageId);
                content.Add(new SetPickItemStoragePlaceRequest { Amount = sp.GetWeightAmount(), Barcode = sp.BatchNumber, StoragePlace = sp.StorageName, Id = x, UserId = CurrentUserId });
            });
        }

        ResultItems.FirstOrDefault(x => x.Id == itemId).Status = 99;
        _pickingHandler.ItemPicked(new SetPickItemRequest { Id = itemId, /*PickerID = long.Parse(CurrentUserId),*/ Status = amcs_shared_lib.Enum.EPickingOrderItemStatus.Kommissioniert, UserId = CurrentUserId, ItemStoragePlaceList = content });

        if (_settings.IsSequentialPicking) countPicking++;

        CloseItem();

        if (_settings.IsSequentialPicking && _resultItem.Position < ResultItems.Count)
        {
            _resultItem = ResultItems[_resultItem.Position];
            if (!_resultItem.IsActive)
            {
                DisplayAlert("Warnung", "Dieser Artikel ist der letzte aktive", "Ok");
                return;
            }

            if (_resultItem.Weighing == 0)
            {
                ShowListItemsPiece(_resultItem);
            }
            else
            {
                ShowListItemsWiege(_resultItem);
            }
        }
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

#region ResultItem ViewCell Model
public class ResultItem : INotifyPropertyChanged
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
                    ColorStatus = App.GetColorResource("StatusIncorrect");
                    IsActive = false;
                    break;
                case 2:
                    ColorStatus = App.GetColorResource("StatusFinish");
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

    public long PickingOrderId { get; private set; }

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
    public long PickingId { get; private set; }
    public string AmountUnit { get; private set; }
    public decimal? Amount { get; set; }
    public decimal? AmountPicked { get; set; }
    public string ItemName { get; private set; }
    public int? Weighing { get; private set; }
    public string UnitName { get; private set; }
    public bool Empties { get; set; }

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

    public ResultItem(PickingOrderItemDTO pickingOrderItemDTO, int index)
    {
        Id = pickingOrderItemDTO.Id;
        Weighing = pickingOrderItemDTO.Weighing is null ? 0 : pickingOrderItemDTO.Weighing;
        if (Weighing == 0) UnitName = AppResources.Amount + ":";
        else UnitName = AppResources.Weight + ":";
        ItemId = pickingOrderItemDTO.ItemId;
        PickingId = pickingOrderItemDTO.PickingId;
        AmountUnit = pickingOrderItemDTO.AmountUnit is null ? "" : pickingOrderItemDTO.AmountUnit;
        Amount = pickingOrderItemDTO.Amount is null ? 1 : Math.Floor(pickingOrderItemDTO.Amount.Value);
        ItemName = pickingOrderItemDTO.ItemName is null ? "No Name" : pickingOrderItemDTO.ItemName;
        IsActive = true;
        ColorTextItem = App.GetColorResource("Black");
        Status = pickingOrderItemDTO.Status;
        Empties = pickingOrderItemDTO.Empties;
        ItemStoragePlaces = pickingOrderItemDTO.ItemStoragePlaces.Any() ? pickingOrderItemDTO.ItemStoragePlaces : new List<ItemStoragePlaceDTO>() { new ItemStoragePlaceDTO
        {
            StorageId = pickingOrderItemDTO.ItemName,
            StoragePlace = pickingOrderItemDTO.ItemName,
            PickItemId = pickingOrderItemDTO.Id
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

public class ResultItemStoragePlace : INotifyPropertyChanged
{
    public string StorageId { get; private set; }
    public string BatchNumber { get; set; }
    public string StorageName { get; set; }
    public int Weighing { get; private set; }

    private string currentValue;
    public string CurrentValue
    {
        get => currentValue;
        set
        {
            currentValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentValue)));
        }
    }
    private double colorActive;
    public double ColorActive
    {
        get => colorActive;
        set
        {
            colorActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorActive)));
        }
    }

    private Color colorStroke;
    public Color ColorStroke
    {
        get => colorStroke;
        set
        {
            colorStroke = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorStroke)));
        }
    }

    private bool isSelectedPlace;

    public bool IsSelectedPlace
    {
        get => isSelectedPlace;
        set
        {
            isSelectedPlace = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectedPlace)));

            /*if (isSelectedPlace) ColorActive = Colors.LightGray;
            else ColorActive = Colors.White;*/
            if (isSelectedPlace)
            {
                ColorActive = 0.8;
                if (App.Current.UserAppTheme == AppTheme.Light) ColorStroke = App.GetColorResource("Black");
                else ColorStroke = App.GetColorResource("White");
            }
            else 
            {
                ColorActive = 0.4;
                ColorStroke = Colors.Transparent;
            }
            
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorStroke)));
        }
    }

    public string PlaceValue { get; set; } = "0";
    public ObservableCollection<WeightPlace> WeightPlaces { get; set; }
    public ResultItemStoragePlace(ItemStoragePlaceDTO itemStoragePlaceDTO)
    {
        StorageId = itemStoragePlaceDTO.StorageId;
        StorageName = itemStoragePlaceDTO.StoragePlace;
        WeightPlaces = new ObservableCollection<WeightPlace>();
        WeightPlaces.Add(new WeightPlace(StorageId, "0"));
        IsSelectedPlace = false;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public Entry Entry { get; set; }

    public decimal GetWeightAmount()
    {
        if (WeightPlaces.Count == 0) return 0;

        decimal weightAmount = 0;
        foreach (var item in WeightPlaces)
        {
            var value = item.PlaceValue.Replace(",", ".");
            if (value != "") weightAmount += decimal.Parse(value);
        }
        return weightAmount;
    }
}

public class WeightPlace : INotifyPropertyChanged
{
    public string StorageId { get; private set; }

    private bool isActive;

    public bool IsActive
    {
        get => isActive;
        set
        {
            isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }
    public string PlaceValue { get; set; }


    private string placeIndex;
    public string PlaceIndex
    {
        get => placeIndex;
        set
        {
            placeIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaceIndex)));
        }
    }

    public WeightPlace(string storageId, string index)
    {
        StorageId = storageId;
        PlaceValue = "";
        PlaceIndex = index;
        IsActive = true;
    }

    public event PropertyChangedEventHandler PropertyChanged;
}

#endregion
public class ButtonPrintingItem : Button
{
    private ContentPage _page; //for testing
    private ResultItem _resultItem;
    private WarehouseClient _warehouse;
    public ButtonPrintingItem(ContentPage page, WarehouseClient warehouse, ResultItem resultItem, Grid grid, int column, int row)
    {
        grid.Add(this);
        grid.SetColumn(this, column);
        grid.SetRow(this, row);
        SetButtonPrinting();
        _page = page;
        _resultItem = resultItem;
        _warehouse = warehouse;
    }

    private void SetButtonPrinting()
    {
        Clicked += async (sender, args) =>
        {
            try
            {
                await _warehouse.PostPrintPickingLabel(new PrintLabelRequest
                {
                    Id = _resultItem.PickingId,
                    PickItemId = _resultItem.Id,
                    LabelType = EPrintLabelTypes.CenterPicking
                });
            }
            catch (Exception ex)
            {
                await _page.DisplayAlert(AppResources.Error, ex.Message, AppResources.Ok);
            }
        };
        Opacity = 0.8;
        CornerRadius = 5;
        WidthRequest = 90;
        Text = AppResources.Print;
        Margin = new Thickness(0, 0, 0, 0);
        Padding = new Thickness(0, 0, 0, 0);

        if (App.Current.UserAppTheme == AppTheme.Light)
        {
            BackgroundColor = App.GetColorResource("Blue100Accent");
            TextColor = App.GetColorResource("White");
        }
        else
        {
            BackgroundColor = App.GetColorResource("Blue300Accent");
            TextColor = App.GetColorResource("Black");
        }
    }
}