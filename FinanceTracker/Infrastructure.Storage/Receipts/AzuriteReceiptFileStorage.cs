using Application.Abstractions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Infrastructure.Storage.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage.Receipts;

public sealed class AzuriteReceiptFileStorage : IReceiptFileStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzuriteStorageOptions _options;

    public AzuriteReceiptFileStorage(BlobServiceClient blobServiceClient, IOptions<AzuriteStorageOptions> options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
    }

    public async Task<ReceiptStoredFileInfo> UploadAsync(
        Stream content,
        string contentType,
        string blobName,
        CancellationToken ct = default)
    {
        var containerName = _options.ReceiptsContainer.Trim().ToLowerInvariant();
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(blobName);
        content.Position = 0;

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            },
            ct);

        BlobProperties properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);

        return new ReceiptStoredFileInfo(
            containerName,
            blobName,
            properties.ContentType ?? contentType,
            properties.ContentLength);
    }

    public async Task<ReceiptStoredFileContent?> OpenReadAsync(
        string containerName,
        string blobName,
        CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        try
        {
            var exists = await blobClient.ExistsAsync(ct);
            if (!exists.Value)
            {
                return null;
            }

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
            var stream = await blobClient.OpenReadAsync(cancellationToken: ct);

            return new ReceiptStoredFileContent(
                stream,
                properties.Value.ContentType ?? "application/octet-stream",
                blobName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
