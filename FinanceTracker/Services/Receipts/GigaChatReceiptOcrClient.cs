using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Results;

namespace Services.Receipts;

public sealed class GigaChatReceiptOcrClient : IReceiptOcrClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GigaChatReceiptOcrClient> _logger;
    private readonly GigaChatReceiptOcrOptions _options;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;

    public GigaChatReceiptOcrClient(
        IHttpClientFactory httpClientFactory,
        IOptions<GigaChatReceiptOcrOptions> options,
        ILogger<GigaChatReceiptOcrClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Result<ReceiptOcrParseResult>> ParseReceiptAsync(
        Stream content,
        string fileName,
        string contentType,
        IReadOnlyCollection<string> categories,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return Result<ReceiptOcrParseResult>.Failure(AppErrors.Validation("GigaChat receipt OCR is disabled."));
        }

        if (string.IsNullOrWhiteSpace(_options.AuthorizationKey))
        {
            return Result<ReceiptOcrParseResult>.Failure(AppErrors.Validation("GigaChat authorization key is not configured."));
        }

        try
        {
            var accessToken = await GetAccessTokenAsync(ct);
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            var uploadedFileId = await UploadFileAsync(accessToken, content, fileName, contentType, ct);
            var prompt = BuildPrompt(categories);
            var rawResponse = await RequestCompletionAsync(accessToken, uploadedFileId, prompt, ct);
            var parseResult = ParseCompletionResponse(rawResponse, uploadedFileId, prompt);
            return Result<ReceiptOcrParseResult>.Success(parseResult);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to parse receipt via GigaChat OCR.");
            return Result<ReceiptOcrParseResult>.Failure(AppErrors.Validation($"GigaChat OCR failed: {exception.Message}"));
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var refreshBoundary = DateTimeOffset.UtcNow.AddSeconds(_options.TokenRefreshSkewSeconds);
        if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > refreshBoundary)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            refreshBoundary = DateTimeOffset.UtcNow.AddSeconds(_options.TokenRefreshSkewSeconds);
            if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > refreshBoundary)
            {
                return _accessToken;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.OAuthUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("RqUID", Guid.NewGuid().ToString());
            request.Headers.TryAddWithoutValidation("Authorization", _options.AuthorizationKey);
            request.Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("scope", _options.Scope)
            ]);

            using var response = await CreateHttpClient().SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            response.EnsureSuccessStatusCode();

            var tokenResponse = JsonSerializer.Deserialize<GigaChatTokenResponse>(responseBody)
                ?? throw new InvalidOperationException("Token response is empty.");

            var token = tokenResponse.AccessToken?.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Access token was not returned by GigaChat.");
            }

            _accessToken = token;
            _accessTokenExpiresAt = ParseExpiry(tokenResponse.ExpiresAt);
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<string> UploadFileAsync(
        string accessToken,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.ApiBaseUrl), "files"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var multipart = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        multipart.Add(fileContent, "file", fileName);
        multipart.Add(new StringContent("general"), "purpose");
        request.Content = multipart;

        using var response = await CreateHttpClient().SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        var fileResponse = JsonSerializer.Deserialize<GigaChatFileUploadResponse>(responseBody)
            ?? throw new InvalidOperationException("File upload response is empty.");

        if (string.IsNullOrWhiteSpace(fileResponse.Id))
        {
            throw new InvalidOperationException("GigaChat did not return an uploaded file id.");
        }

        return fileResponse.Id;
    }

    private async Task<string> RequestCompletionAsync(
        string accessToken,
        string uploadedFileId,
        string prompt,
        CancellationToken ct)
    {
        var payload = new
        {
            model = _options.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt,
                    attachments = new[] { uploadedFileId }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.ApiBaseUrl), "chat/completions"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await CreateHttpClient().SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        return responseBody;
    }

    private ReceiptOcrParseResult ParseCompletionResponse(string rawResponse, string uploadedFileId, string prompt)
    {
        using var document = JsonDocument.Parse(rawResponse);
        var content = ExtractAssistantContent(document.RootElement);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("GigaChat completion content is empty.");
        }

        string? merchant = null;
        DateTime? purchaseDate = null;
        decimal? totalAmount = null;
        string? totalCurrencyCode = null;
        var items = new List<ReceiptOcrItemResult>();

        var lines = content
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line) && line != "```")
            .ToList();

        foreach (var line in lines)
        {
            if (line.StartsWith("MERCHANT|", StringComparison.OrdinalIgnoreCase))
            {
                merchant = line["MERCHANT|".Length..].Trim();
                continue;
            }

            if (line.StartsWith("DATE|", StringComparison.OrdinalIgnoreCase))
            {
                var dateValue = line["DATE|".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(dateValue))
                {
                    if (!DateTime.TryParse(dateValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
                    {
                        throw new InvalidOperationException($"Failed to parse receipt date from line: {line}");
                    }

                    purchaseDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                }

                continue;
            }

            if (line.StartsWith("TOTAL|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('|');
                if (parts.Length != 3)
                {
                    throw new InvalidOperationException($"Invalid TOTAL line format: {line}");
                }

                if (!string.IsNullOrWhiteSpace(parts[1]))
                {
                    totalAmount = ParseDecimal(parts[1], line);
                }

                totalCurrencyCode = string.IsNullOrWhiteSpace(parts[2]) ? null : NormalizeCurrencyCode(parts[2], line);
                continue;
            }

            if (line.StartsWith("ITEM|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('|');
                if (parts.Length != 5)
                {
                    throw new InvalidOperationException($"Invalid ITEM line format: {line}");
                }

                var itemName = parts[1].Trim();
                var currencyCode = NormalizeCurrencyCode(parts[2], line);
                var price = ParseDecimal(parts[3], line);
                var categoryName = parts[4].Trim();

                if (string.IsNullOrWhiteSpace(itemName)
                    || string.IsNullOrWhiteSpace(currencyCode)
                    || string.IsNullOrWhiteSpace(categoryName))
                {
                    throw new InvalidOperationException($"Receipt item line contains empty fields: {line}");
                }

                items.Add(new ReceiptOcrItemResult(itemName, currencyCode, price, categoryName));
                continue;
            }

            throw new InvalidOperationException($"Unexpected OCR response line: {line}");
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException("OCR response did not contain any ITEM rows.");
        }

        return new ReceiptOcrParseResult(
            merchant,
            purchaseDate,
            totalAmount,
            totalCurrencyCode,
            uploadedFileId,
            prompt,
            rawResponse,
            items);
    }

    private string BuildPrompt(IReadOnlyCollection<string> categories)
    {
        var categoryList = categories.Count == 0
            ? "Others"
            : string.Join(", ", categories.OrderBy(category => category, StringComparer.OrdinalIgnoreCase));

        return _options.PromptTemplate.Replace("{categories}", categoryList, StringComparison.Ordinal);
    }

    private static string ExtractAssistantContent(JsonElement rootElement)
    {
        if (TryGetAssistantContent(rootElement, out var directContent))
        {
            return directContent;
        }

        if (rootElement.TryGetProperty("value", out var valueElement) && TryGetAssistantContent(valueElement, out var wrappedContent))
        {
            return wrappedContent;
        }

        return string.Empty;
    }

    private static bool TryGetAssistantContent(JsonElement element, out string content)
    {
        content = string.Empty;
        if (!element.TryGetProperty("choices", out var choicesElement) || choicesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var choice in choicesElement.EnumerateArray())
        {
            if (!choice.TryGetProperty("message", out var messageElement))
            {
                continue;
            }

            if (messageElement.TryGetProperty("content", out var contentElement))
            {
                content = contentElement.GetString() ?? string.Empty;
                return true;
            }
        }

        return false;
    }

    private static decimal ParseDecimal(string value, string line)
    {
        var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidOperationException($"Failed to parse decimal value from line: {line}");
        }

        return result;
    }

    private static string NormalizeCurrencyCode(string value, string line)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Currency code is empty in line: {line}");
        }

        if (CurrencyCodeNormalizer.TryNormalize(value, out var normalized))
        {
            return normalized;
        }

        throw new InvalidOperationException($"Unsupported currency code '{value}' in line: {line}");
    }

    private static DateTimeOffset ParseExpiry(long expiresAt)
    {
        return expiresAt > 100_000_000_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(expiresAt)
            : DateTimeOffset.FromUnixTimeSeconds(expiresAt);
    }

    private HttpClient CreateHttpClient()
    {
        return _httpClientFactory.CreateClient("GigaChatReceiptOcr");
    }

    private sealed class GigaChatTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }
    }

    private sealed class GigaChatFileUploadResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
