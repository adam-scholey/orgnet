using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;

namespace OrgNet.Pwa.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _storage;

    public ApiClient(HttpClient http, ILocalStorageService storage)
    {
        _http = http;
        _storage = storage;
    }

    private async Task AttachHeadersAsync()
    {
        var token = await _storage.GetItemAsStringAsync("access_token");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tenantId = await _storage.GetItemAsStringAsync("tenant_id");
        if (!string.IsNullOrEmpty(tenantId))
        {
            _http.DefaultRequestHeaders.Remove("X-Tenant-Id");
            _http.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        }
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        await AttachHeadersAsync();
        try
        {
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch { return default; }
    }

    public async Task<T?> PostAsync<T>(string url, object data)
    {
        await AttachHeadersAsync();
        try
        {
            var response = await _http.PostAsJsonAsync(url, data);
            if (!response.IsSuccessStatusCode)
            {
                try { return await response.Content.ReadFromJsonAsync<T>(); }
                catch { return default; }
            }
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch { return default; }
    }

    public async Task<T?> PutAsync<T>(string url, object data)
    {
        await AttachHeadersAsync();
        try
        {
            var response = await _http.PutAsJsonAsync(url, data);
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch { return default; }
    }

    public async Task<bool> DeleteAsync(string url)
    {
        await AttachHeadersAsync();
        try
        {
            var response = await _http.DeleteAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
