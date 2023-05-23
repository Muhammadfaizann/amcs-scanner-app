using amcs_shared_lib.Models.DTO;
using amcs_shared_lib.Models.HTTP;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace amcs_scanner_app.Client;

public class InventoryClient
{
    private readonly AppSettings _settings;
    private readonly ILogger<InventoryClient> _logger;
    private readonly HttpClient _client;

    public InventoryClient(AppSettings settings, ILogger<InventoryClient> logger)
    {
        _settings = settings;
        _logger = logger;
        //_client = new HttpClient() { BaseAddress = new Uri(settings.InventorySettings.BaseUrl) };

        //AuthenticateClient();
    }


    public async void AuthenticateClient()
    {
        if (await SecureStorage.GetAsync("IsAuthenticated") != "true")
            throw new UnauthorizedAccessException();

        var token = await SecureStorage.GetAsync("BearerToken");

        if (string.IsNullOrEmpty(token))
            throw new UnauthorizedAccessException();

        _client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
    }

    public async Task<bool> CheckConnection()
    {
        try
        {
            var response = await _client.GetAsync(_settings.InventorySettings.PingUrl);

            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<GetInventoryListResponse> GetInventoryListById(long inventoryListId) 
    {
        try
        {
            var response = await _client.GetAsync(_settings.InventorySettings.GetInventoryListByIdUrl + "/" + inventoryListId + "/All"); 
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Bad Request. Failed to load inventory list with id: " + inventoryListId);
            return await response.Content.ReadFromJsonAsync<GetInventoryListResponse>(); 
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Order!", e);
            return null;
        }
    }

    public async Task<GetInventoryListResponse> GetInventoryListtByStatus(GetInventoryListByStatusRequest request) 
    {
        try
        {
            var response = await _client.GetAsync(_settings.InventorySettings.GetInventoryOrdersByStatusUrl + "/" + ((int)request.Status) + "/All"); 
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Bad Request");
                
            return await response.Content.ReadFromJsonAsync<GetInventoryListResponse>(); 
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    public async Task<GetInventoryListResponse> GetInventoryListByStatusWithLimit(GetInventoryListByStatusRequest request, int limit) 
    {
        try
        {
            var response = await _client.GetAsync(_settings.InventorySettings.GetInventoryOrdersByStatusWithLimitUrl + "/" + ((int)request.Status) + "/" + limit);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GetInventoryListResponse>();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    public async Task<List<InventoryOrderOverviewDTO>> GetInventoryListOverview(int status = 1) 
    {
        try
        {
            var result = await _client.GetAsync(_settings.InventorySettings.GetInventoryOrdersByStatusWithLimitUrl + "/1/" + _settings.Configuration.MaxListedPickingOrders);
            result.EnsureSuccessStatusCode();
            var content = await result.Content.ReadFromJsonAsync<GetInventoryListOverviewResponse>();  
            return content.PickingOrderOverview;                                            // !!!   Korrigieren in amcs_shared_lib.Models.HTTP.GetInventoryListOverviewResponse: PickingOrderOverview --> InventoryOrderOverview
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    public async Task<GetInventoryListResponse> GetInventoryListByUser(GetPicklistByUserRequest request) 
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.InventorySettings.GetInventoryOrdersByUserUrl, request);  
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Bad Request");

            return await response.Content.ReadFromJsonAsync<GetInventoryListResponse>();   
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    public async Task<GetInventoryListResponse> GetInventoryList(GetInventoryListRequest request)   
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.InventorySettings.GetInventoryListByIdUrl, request); 
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GetInventoryListResponse>();  
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    public async Task<SetInventoryItemResponse> PostSetItemInventoried(SetInventoryItemRequest request) 
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.InventorySettings.PostSetInventoryItemUrl, request);  

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SetInventoryItemResponse>();  
        }    
        catch (Exception e)
        {
            _logger.LogError("Failed to send!", e);
            return null;
        }
    }

    public async Task<SetInventoryListRespons> PostSetInventoryList(SetInventoryListRequest request)  // !!!   Korrigieren in amcs_shared_lib.Models.HTTP: SetInventoryListRespons --> SetInventoryListRespons + "e"
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.InventorySettings.PostSetInventoryOrderUrl, request);  
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Bad Request");

            return await response.Content.ReadFromJsonAsync<SetInventoryListRespons>();   
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to send!", e);
            return null;
        }
    }

}
