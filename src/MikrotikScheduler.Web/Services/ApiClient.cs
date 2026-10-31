using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikrotikScheduler.Web.Services;

public class ApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>Typed client for MikrotikScheduler.Api. Forwards the signed-in user's JWT.</summary>
public class ApiClient(HttpClient http, IHttpContextAccessor accessor)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<T> GetAsync<T>(string url, CancellationToken ct = default) => (await SendAsync<T>(HttpMethod.Get, url, null, ct))!;
    public Task<T?> PostAsync<T>(string url, object? body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Post, url, body, ct);
    public Task<T?> PutAsync<T>(string url, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Put, url, body, ct);
    public Task<T?> PatchAsync<T>(string url, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Patch, url, body, ct);
    public Task DeleteAsync(string url, CancellationToken ct = default) => SendAsync<object>(HttpMethod.Delete, url, null, ct);

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, url);
        var token = accessor.HttpContext?.User.FindFirst("access_token")?.Value;
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) req.Content = JsonContent.Create(body, options: Json);

        HttpResponseMessage res;
        try { res = await http.SendAsync(req, ct); }
        catch (HttpRequestException ex) { throw new ApiException(HttpStatusCode.ServiceUnavailable, $"API is unreachable: {ex.Message}"); }

        using (res)
        {
            if (!res.IsSuccessStatusCode) throw new ApiException(res.StatusCode, await ReadErrorAsync(res, ct));
            if (res.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object)) return default;
            return await res.Content.ReadFromJsonAsync<T>(Json, ct);
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage res, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                return string.Join(" ", errors.EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString())));
            if (root.TryGetProperty("detail", out var d) && d.GetString() is { } s) return s;
            if (root.TryGetProperty("title", out var t) && t.GetString() is { } ts) return ts;
        }
        catch { /* not JSON */ }
        return res.StatusCode == HttpStatusCode.Unauthorized ? "Session expired. Please sign in again." : $"Request failed ({(int)res.StatusCode}).";
    }
}
