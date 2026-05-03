namespace Infrastructure.Storage.Configuration;

public sealed class AzuriteStorageOptions
{
    public const string SectionName = "AzuriteStorage";

    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";
    public string ReceiptsContainer { get; set; } = "receipts";
}
