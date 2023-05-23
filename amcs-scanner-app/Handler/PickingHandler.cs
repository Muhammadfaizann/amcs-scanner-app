using amcs_scanner_app.Client;
using amcs_scanner_app.Helper;
using amcs_shared_lib.Cache;
using amcs_shared_lib.Models.HTTP;

namespace amcs_scanner_app.Handler;
/// <summary>
/// The picking handler should process the [PickingAction] cache in the background in a configurable cycle
/// </summary>
public class PickingHandler
{
    private readonly WarehouseClient _warehouseClient;
    private readonly MessageCache<SetPickItemRequest> _itemCache;
    public readonly MessageCache<SetPicklistRequest> _orderCache;
    private readonly BackgroundTask _task;

    public EventHandler<ItemPickedEventArgs> ItemPickedEvent;
    public PickingHandler(WarehouseClient warehouseClient, MessageCache<SetPickItemRequest> itemCache, MessageCache<SetPicklistRequest> orderCache, AppSettings settings)
    {
        _warehouseClient = warehouseClient;
        _itemCache = itemCache;
        _orderCache = orderCache;
        _task = new BackgroundTask(TimeSpan.FromSeconds(1), ClearCacheAsync);
    }

    /// <summary>
    /// Starts _task
    /// </summary>
    public void Start()
    {
        _task.Start();
    }
    /// <summary>
    /// Stops _task
    /// </summary>
    public void Stop()
    {
        _task.Stop();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task ClearCacheAsync()
    {
        // loads items from cache
        var itemCacheContent = _itemCache.GetCachedMessages().ToList();
        var orderCacheContent = _orderCache.GetCachedMessages().ToList();

        if (itemCacheContent.Any())
        {
            // sorted by createdTime
            itemCacheContent.Sort((x, y) => DateTime.Compare(x.Created, y.Created));

            // send objects
            foreach (var item in itemCacheContent)
            {
                try
                {
                    var response = await _warehouseClient.PostSetItemPicked(item);

                    // only delets item from chache where response.Success == true
                    if (response.Success)
                    {
                        _itemCache.Delete(item);
                        ItemPickedEvent?.Invoke(this, new ItemPickedEventArgs { Id = item.Id });
                    }
                }
                catch (Exception ex) { }
            }
        }
        if (orderCacheContent.Any())
        {
            // check Connection
            if (!await _warehouseClient.CheckConnection())
                return;

            // sort by createdTime
            orderCacheContent.Sort((x, y) => DateTime.Compare(x.Created, y.Created));

            // send object
            foreach (var order in orderCacheContent)
            {
                try
                {
                    var response = await _warehouseClient.PostSetPicklist(order);

                    // only delets item from chache where response.Success == true
                    if (response.Success)
                    {
                        _orderCache.Delete(order);
                    }
                }
                catch (Exception ex) { }
            }
        }
    }

    public void ItemPicked(SetPickItemRequest request) =>
        _itemCache.Write(request);

    public void OrderFinished(SetPicklistRequest request) => 
        _orderCache.Write(request);
}

public class ItemPickedEventArgs : EventArgs
{
    public long Id { get; set; }
}