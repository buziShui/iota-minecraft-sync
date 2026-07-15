using System.Net.Http.Json;
using System.Text.Json;

namespace IotaSync.Docker;

public sealed class MslxClient
{
    private readonly HttpClient _httpClient;
    private readonly Uri? _baseUri;
    private readonly string _apiKey;

    public bool Configured => _baseUri is not null && !string.IsNullOrWhiteSpace(_apiKey);
    public string PublicUrl { get; }
    public string? ConfigurationError { get; }

    public MslxClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["IOTA_MSLX_API_KEY"]?.Trim() ?? string.Empty;
        var baseUrl = configuration["IOTA_MSLX_BASE_URL"]?.Trim() ?? string.Empty;
        PublicUrl = configuration["IOTA_MSLX_PUBLIC_URL"]?.Trim() ?? baseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(_apiKey))
        {
            ConfigurationError = "尚未配置 MSLX 连接地址或 API Key";
            return;
        }

        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ConfigurationError = "IOTA_MSLX_BASE_URL 必须是有效的 HTTP(S) 绝对地址";
            return;
        }

        _baseUri = uri;
    }

    public Task<JsonElement> GetDataAsync(string path, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, path, null, cancellationToken);

    public Task<JsonElement> PostDataAsync(string path, object body, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, path, body, cancellationToken);

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        if (!Configured)
            throw new MslxIntegrationException(ConfigurationError ?? "MSLX 集成尚未配置");

        using var request = new HttpRequestMessage(method, new Uri(_baseUri!, path.TrimStart('/')));
        request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        if (body is not null) request.Content = JsonContent.Create(body);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new MslxIntegrationException("无法连接 MSLX Daemon，请检查服务状态和连接地址", exception);
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(content);
            }
            catch (JsonException exception)
            {
                throw new MslxIntegrationException($"MSLX 返回了无法识别的响应（HTTP {(int)response.StatusCode}）", exception);
            }

            using (document)
            {
                var root = document.RootElement;
                var message = root.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : null;
                var apiCode = root.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var parsedCode)
                    ? parsedCode
                    : (int)response.StatusCode;

                if (!response.IsSuccessStatusCode || apiCode is < 200 or >= 300)
                    throw new MslxIntegrationException(message ?? $"MSLX 请求失败（{apiCode}）");

                return root.TryGetProperty("data", out var dataElement) ? dataElement.Clone() : root.Clone();
            }
        }
    }
}

public sealed class MslxIntegrationException : Exception
{
    public MslxIntegrationException(string message) : base(message) { }
    public MslxIntegrationException(string message, Exception innerException) : base(message, innerException) { }
}
