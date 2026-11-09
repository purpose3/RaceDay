using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceDay.Web.Services;

public readonly record struct Unit;
public record ImageData(byte[] Bytes, string ContentType);

public record ApiResult<T>(bool Success, T? Data, int StatusCode, string? Error)
{
    public bool IsUnauthorized => StatusCode == 401;
    public bool IsNotFound => StatusCode == 404;
}

/// <summary>
/// The only way this application reaches data: calls to the Part 2 API.
/// Adds the signed-in user's JWT, turns failures into friendly messages and never throws to the caller.
/// </summary>
public class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly UserSession _session;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, UserSession session, ILogger<ApiClient> logger)
    {
        _http = http;
        _session = session;
        _logger = logger;
    }

    public Task<ApiResult<T>> GetAsync<T>(string url) =>
        SendAsync(HttpMethod.Get, url, null, ReadJsonAsync<T>);

    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null) =>
        SendAsync(HttpMethod.Post, url, JsonBody(body ?? new { }), ReadJsonAsync<T>);

    public Task<ApiResult<T>> PutAsync<T>(string url, object body) =>
        SendAsync(HttpMethod.Put, url, JsonBody(body), ReadJsonAsync<T>);

    public Task<ApiResult<Unit>> DeleteAsync(string url) =>
        SendAsync<Unit>(HttpMethod.Delete, url, null, _ => Task.FromResult<Unit>(default));

    public Task<ApiResult<T>> PostFileAsync<T>(string url, IFormFile file)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var ct)
            ? ct
            : new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", file.FileName);
        return SendAsync(HttpMethod.Post, url, content, ReadJsonAsync<T>);
    }

    public Task<ApiResult<ImageData>> GetImageAsync(string url) =>
        SendAsync<ImageData>(HttpMethod.Get, url, null, async r =>
            new ImageData(await r.Content.ReadAsByteArrayAsync(),
                r.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"));

    // ---------- internals ----------

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json");

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage r)
    {
        if (r.StatusCode == HttpStatusCode.NoContent || r.Content.Headers.ContentLength == 0) return default;
        return await r.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string url, HttpContent? content, Func<HttpResponseMessage, Task<T?>> read)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url) { Content = content };
            var token = _session.Token;
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request);
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
                return new ApiResult<T>(true, await read(response), status, null);

            // The API rejected our token (expired or invalid): end the session
            if (status == 401 && !string.IsNullOrEmpty(token)) _session.SignOut();

            return new ApiResult<T>(false, default, status, await ReadErrorAsync(response, status));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            _logger.LogError(ex, "API call failed: {Method} {Url}", method, url);
            return new ApiResult<T>(false, default, 0,
                "We couldn't reach the RaceDay service. Please try again in a moment.");
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, int status)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    // Our API's own messages: { "error": "..." }
                    if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                        return err.GetString()!;

                    // Automatic validation errors: { "errors": { "Field": ["message"] } }
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var field in errors.EnumerateObject())
                            foreach (var message in field.Value.EnumerateArray())
                                if (message.ValueKind == JsonValueKind.String)
                                    return message.GetString()!;
                    }
                }
            }
        }
        catch (JsonException) { /* fall through to the generic message */ }

        return status switch
        {
            400 => "Please check the information you entered and try again.",
            401 => "Your session has expired. Please log in again.",
            403 => "You don't have permission to do that.",
            404 => "We couldn't find what you were looking for.",
            409 => "That action conflicts with the current state. Please refresh and try again.",
            >= 500 => "Something went wrong on our side. Please try again in a moment.",
            _ => "Something went wrong. Please try again."
        };
    }
}
