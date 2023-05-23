using amcs_shared_lib.Models.DTO;
using amcs_shared_lib.Models.Delta;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using amcs_shared_lib.Models.HTTP;

namespace amcs_scanner_app.Client;
/// <summary>
/// Methods are created in the client to communicate with the Warehouse Service
/// </summary>
public class WarehouseClient
{
    private readonly AppSettings _settings;
    private readonly ILogger<WarehouseClient> _logger;
    private readonly HttpClient _client;

    public WarehouseClient(AppSettings settings, ILogger<WarehouseClient> logger)
    {
        _settings = settings;
        _logger = logger;
        _client = new HttpClient() { BaseAddress = new Uri(settings.WarehouseSettings.BaseUrl) };

        AuthenticateClient();
    }
    /// <summary>
    /// gets the BearerToken from SecureStorage and checks if the Token has Values.
    /// When The BearerToken has Values, its added to "_client" as Key Value   ( Authorization => "Bearer"BearerToken Value) 
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">if token is null or IsAuthenticated is false</exception>
    public async void AuthenticateClient()
    {
        if (await SecureStorage.GetAsync("IsAuthenticated") != "true")
            throw new UnauthorizedAccessException();

        var token = await SecureStorage.GetAsync("BearerToken");

        if (string.IsNullOrEmpty(token))
            throw new UnauthorizedAccessException();

        _client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
    }

    /// <summary>
    /// checks the Connection to Warehouse Service
    /// </summary>
    /// <returns>true</returns>if successfull
    /// <returns>false</returns> if failed
    public async Task<bool> CheckConnection()
    {
        try
        {
            var response = await _client.GetAsync(_settings.WarehouseSettings.PingUrl);

            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="picklistId"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<GetPicklistResponse> GetPicklistById(long picklistId)
    {
        try
        {
            var response = await _client.GetAsync(_settings.WarehouseSettings.GetPicklistByIdUrl + "/" + picklistId + "/All");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Bad Request. Failed to load Picklist with id: " + picklistId);
            return await response.Content.ReadFromJsonAsync<GetPicklistResponse>();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Order!", e);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<GetPicklistResponse> GetPicklistByStatus(GetPicklistByStatusRequest request)
    {
        try
        {
            var response = await _client.GetAsync(_settings.WarehouseSettings.GetPickingOrdersByStatusUrl + "/" + ((int) request.Status) + "/All");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Bad Request");

            return await response.Content.ReadFromJsonAsync<GetPicklistResponse>();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    public async Task<GetPicklistResponse> GetPicklistByStatusWithLimit(GetPicklistByStatusRequest request, int limit)
    {
        try
        {
            var response = await _client.GetAsync(_settings.WarehouseSettings.GetPickingOrdersByStatusWithLimitUrl + "/" + ((int)request.Status) + "/" + limit);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GetPicklistResponse>();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public async Task<List<PickingOrderOverviewDTO>> GetPicklistOverview(int status = 1)
    {
        try
        {
            var result = await _client.GetAsync(_settings.WarehouseSettings.GetPickingOrderOverviewWithLimitUrl + "/1/" + _settings.Configuration.MaxListedPickingOrders);
            result.EnsureSuccessStatusCode();
            var content = await result.Content.ReadFromJsonAsync<GetPicklistOverviewResponse>();
            return content.PickingOrderOverview;
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<GetPicklistResponse> GetPicklistByUser(GetPicklistByUserRequest request)
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.WarehouseSettings.GetPickingOrdersByUserUrl, request);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Bad Request");

            return await response.Content.ReadFromJsonAsync<GetPicklistResponse>();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<GetPicklistResponse> GetPicklists(GetPicklistRequest request)
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.WarehouseSettings.GetPicklistByIdUrl, request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GetPicklistResponse>();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to get Orders!", e);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<SetPickItemResponse> PostSetItemPicked(SetPickItemRequest request)
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.WarehouseSettings.PostSetPickingItemUrl, request);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SetPickItemResponse>();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to send!", e);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<SetPicklistResponse> PostSetPicklist(SetPicklistRequest request)
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.WarehouseSettings.PostSetPickingOrderUrl, request);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Bad Request");

            return await response.Content.ReadFromJsonAsync<SetPicklistResponse>();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to send!", e);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<PrintLabelResponse> PostPrintPickingLabel(PrintLabelRequest request)
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.WarehouseSettings.PostPrintPickingLabel, request);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Bad Request");

            return await response.Content.ReadFromJsonAsync<PrintLabelResponse>();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to send!", e);
            return null;
        }
    }
    

}
