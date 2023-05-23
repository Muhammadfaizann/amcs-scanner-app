using amcs_scanner_app.Client;
using amcs_scanner_app.Helper;
using amcs_shared_lib.Cache;
using amcs_shared_lib.Models.HTTP;

namespace amcs_scanner_app.Handler;

public class InventoryHandler
{
    private readonly InventoryClient _inventoryClient;
    private readonly MessageCache<SetInventoryItemRequest> _itemCache;  
    public readonly MessageCache<SetInventoryListRequest> _orderCache;  
    private readonly BackgroundTask _task;

    public EventHandler<ItemInventoriedEventArgs> ItemInventoriedEvent;
    public InventoryHandler (InventoryClient inventoryClient, MessageCache<SetInventoryItemRequest> itemCache, MessageCache<SetInventoryListRequest> orderCache, AppSettings settings)  
    {
        _inventoryClient = inventoryClient;
        _itemCache = itemCache;
        _orderCache = orderCache;
        _task = new BackgroundTask(TimeSpan.FromSeconds(1), ClearCacheAsync);
    }

    public void Start()
    {
        _task.Start();
    }

    public void Stop()
    {
        _task.Stop();
    }

    public async Task ClearCacheAsync()
    {
        // Objekte aus Cache laden
        var itemCacheContent = _itemCache.GetCachedMessages().ToList();
        var orderCacheContent = _orderCache.GetCachedMessages().ToList();

        if (itemCacheContent.Any())
        {
            // Nach Erstellungszeit sortieren
            itemCacheContent.Sort((x, y) => DateTime.Compare(x.Created, y.Created));

            // Objekte senden
            foreach (var item in itemCacheContent)
            {
                try
                {
                    var response = await _inventoryClient.PostSetItemInventoried(item);

                    // Nur von cache löschen, wenn response.Success == true ist
                    if (response.Success)
                    {
                        _itemCache.Delete(item);
                        ItemInventoriedEvent?.Invoke(this, new ItemInventoriedEventArgs { Id = item.Id });
                    }
                }
                catch (Exception ex) { }
            }
        }

        if (orderCacheContent.Any())
        {
            // Ping Service
            if (!await _inventoryClient.CheckConnection())
                return;

            // Nach Erstellungszeit sortieren
            orderCacheContent.Sort((x, y) => DateTime.Compare(x.Created, y.Created));

            // Objekte senden
            foreach (var order in orderCacheContent)
            {
                try
                {
                    var response = await _inventoryClient.PostSetInventoryList(order);

                    // Nur von cache löschen, wenn response.Success == true ist
                    if (response.Success)
                    {
                        _orderCache.Delete(order);
                    }
                }
                catch (Exception ex) { }
            }
        }
    }

    public void ItemPicked(SetInventoryItemRequest request) => 
        _itemCache.Write(request);

    public void OrderFinished(SetInventoryListRequest request) =>   
        _orderCache.Write(request);

}

public class ItemInventoriedEventArgs : EventArgs
{
    public long Id { get; set; }
}
