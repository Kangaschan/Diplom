namespace Services.Receipts;

public sealed class GigaChatReceiptOcrOptions
{
    public const string SectionName = "GigaChatReceiptOcr";

    public bool Enabled { get; set; }
    public bool IgnoreSslErrors { get; set; }
    public string OAuthUrl { get; set; } = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
    public string ApiBaseUrl { get; set; } = "https://gigachat.devices.sberbank.ru/api/v1/";
    public string AuthorizationKey { get; set; } = string.Empty;
    public string Scope { get; set; } = "GIGACHAT_API_PERS";
    public string Model { get; set; } = "GigaChat-2-Max";
    public string PromptTemplate { get; set; } = string.Empty;
    public int TokenRefreshSkewSeconds { get; set; } = 60;
}
