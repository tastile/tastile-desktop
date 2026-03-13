namespace TastileDesktop.Services;

using System.Net.Http;
using System.Net.Http.Json;

public class CoreApiClient
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:3140";

    public CoreApiClient()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
