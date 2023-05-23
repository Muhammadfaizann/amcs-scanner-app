using amcs_shared_lib.Enum;
using amcs_shared_lib.Models.HTTP;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace amcs_scanner_app.Client;
/// <summary>
/// 
/// </summary>
public class UserServiceClient
{
    private readonly AppSettings _settings;
    private readonly ILogger<UserServiceClient> _logger;
    private HttpClient _client;

    public UserServiceClient(AppSettings appSettings, ILogger<UserServiceClient> logger)
    {
        _settings = appSettings; _logger = logger;
    }

    /// <summary>
	/// initilizes a HttpClient to the BaseAdress wich is setted in the _settings
	/// </summary>
	/// <returns>true</returns>if an httpClient _client is initilized
	/// <returns>false</returns> if an exception is thrown.
    public bool Connect()
    {
        try
        {
            _client = new HttpClient() { BaseAddress = new Uri(_settings.UserServiceSettings.BaseUrl) };
            return true;
        }
        catch (Exception e) { return false; }
    }

    /// <summary>
    /// checks if the SecureStorage key "IsAuthenticated" is true
    /// </summary>
    /// <returns>true</returns> if the SecureStorage  Key IsAuthenticated is true
    /// <returns>false</returns> if the SecureStorage  Key IsAuthenticated is false
    public async Task<bool> IsAuthenticated() =>
        await SecureStorage.GetAsync("IsAuthenticated") == "true" ? true : false;


    /// <summary>
    /// authenticates with the Userservice
    /// checks if the UserId is verified
    /// and sets the UserId in the SecureStorage
    /// </summary>
    /// <param name="userId"></param>
    /// <returns>true</returns>  if the AuthenticateUserResponse is true
    /// <returns>false</returns> if the AuthenticateUserResponse is false or an Exception is Thrown
    public async Task<bool> Authenticate(string userId)
    {
        try
        {
            var response = await _client.PostAsJsonAsync(_settings.UserServiceSettings.AuthenticationUrl, new AuthenticateUserRequest { UserId = userId });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AuthenticateUserResponse>();
            if (result.Success)
            {
                SecureStorage.RemoveAll();
                await SecureStorage.SetAsync("BearerToken", result.Token);
                await SecureStorage.SetAsync("IsAuthenticated", "true");
                //await SecureStorage.SetAsync("UserName", result.FullName);
                await SecureStorage.SetAsync("UserId", userId);

                if (result.Employee.Roles.Count != 0)
                    foreach (var item in result.Employee.Roles)
                    {
                        foreach (var right in item.Rights)
                        {
                            await SetRoles(right);
                        }
                    }
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (Exception e)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks the Right and sets a SecureStorage permission
    /// if no rights is sent,   the Securestorage permission on None
    /// </summary>
    /// <param name="permissions"></param>
    /// <returns></returns>
    private Task SetRoles(ERights permissions) =>
        permissions switch
        {
            ERights.Admin => SecureStorage.SetAsync("IsAdmin", "true"),
            ERights.PerformPicking => SecureStorage.SetAsync("IsPerformPickings", "true"),
            ERights.ControlePicking => SecureStorage.SetAsync("IsControlePickings", "true"),
            ERights.ManagePicking => SecureStorage.SetAsync("IsManagePickings", "true"),
            ERights.PerformInventory => SecureStorage.SetAsync("IsPerformInventory", "true"),
            ERights.ManageInventory => SecureStorage.SetAsync("IsManageInventory", "true"),
            ERights.ManageEmployees => SecureStorage.SetAsync("IsManageEmployees", "true"),
            _ => SecureStorage.SetAsync("None", "None"),
        };
}
