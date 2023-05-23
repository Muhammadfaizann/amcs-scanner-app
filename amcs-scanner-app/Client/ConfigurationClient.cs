using amcs_shared_lib.Helper;
using amcs_shared_lib.Models.Configuration;
using System.Net.Http.Json;

namespace amcs_scanner_app.Client;
public class ConfigurationClient
{
    private readonly AppSettings _settings;
    private HttpClient _client;

    public ConfigurationClient(AppSettings settings)
    {
        _settings = settings;
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
            _client = new HttpClient() { BaseAddress = new Uri(_settings.ConfigurationSettings.BaseUrl) };
            return true;
        }
        catch (Exception ex) { return false; }
    }

    /// <summary>
    /// gets the appsettings from the _client async 
    /// initilizes appsettings as a SettingsHelper from the shared_lib
    /// gets the configuration from the Appsettings and saves it in the _settings.
    /// </summary>
    /// <returns>true</returns> if _settings.Configuration can be setted
    /// <returns>false</returns> if an Exception is thrown.
    public async Task<bool> LoadSettings()
    {
        try
        {
            var response = await _client.GetAsync(_settings.ConfigurationSettings.GetAllByGroup + "/appsettings");

            var appSettings = new SettingsHelper<amcs_shared_lib.Models.Configuration.Groups.AppSettings>();

            _settings.Configuration = appSettings.GetObject(await response.Content.ReadFromJsonAsync<List<ConfigurationParameter>>());

            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }
}
