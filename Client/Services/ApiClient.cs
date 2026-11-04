using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared.Dtos;

namespace Client.Services;

/// <summary>
/// HTTP-клиент к серверу. Автоматически подставляет Authorization: Bearer {token}
/// из SessionService, десериализует JSON в DTO, разбирает тело ошибки.
/// </summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly SessionService _session;
    private readonly string _baseUrl;

    public ApiClient(HttpClient http, SessionService session, string baseUrl)
    {
        _http = http;
        _session = session;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<LoginResponse> LoginAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        var request = new LoginRequest { Login = login, Password = password };
        using var response = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/login", request, JsonOptions, cancellationToken);
        return await HandleAsync<LoginResponse>(response, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using var req = CreateRequest(HttpMethod.Post, $"{_baseUrl}/api/auth/logout");
        using var response = await _http.SendAsync(req, cancellationToken);
        _ = response.StatusCode;
    }

    public Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);

    public Task<T> PostAsync<T>(string path, object? body = null, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, path, body, cancellationToken);

    public Task<T> PutAsync<T>(string path, object body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Put, path, body, cancellationToken);

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var req = CreateRequest(HttpMethod.Delete, $"{_baseUrl}{path}");
        using var response = await _http.SendAsync(req, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(await ReadErrorAsync(response, cancellationToken), (int)response.StatusCode);
        }
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var req = CreateRequest(method, $"{_baseUrl}{path}");
        if (body != null)
        {
            req.Content = JsonContent.Create(body, options: JsonOptions);
        }
        using var response = await _http.SendAsync(req, cancellationToken);
        return await HandleAsync<T>(response, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        if (_session.IsAuthenticated)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        }
        return req;
    }

    private static async Task<T> HandleAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(await ReadErrorAsync(response, cancellationToken), (int)response.StatusCode);
        }
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default!;
        }
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new ApiException("Сервер вернул пустой ответ.", (int)response.StatusCode);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return $"Ошибка сервера: {(int)response.StatusCode} {response.ReasonPhrase}.";
            }
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                return err.GetString() ?? body;
            }
            if (doc.RootElement.TryGetProperty("title", out var title))
            {
                return title.GetString() ?? body;
            }
            return body;
        }
        catch
        {
            return $"Ошибка сервера: {(int)response.StatusCode} {response.ReasonPhrase}.";
        }
    }
}

/// <summary>Исключение, содержащее HTTP-статус и сообщение от сервера.</summary>
public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(string message, int statusCode) : base(message) => StatusCode = statusCode;
}
