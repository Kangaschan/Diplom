namespace Application.Abstractions;

public sealed record ReceiptStoredFileInfo(
    string ContainerName,
    string BlobName,
    string ContentType,
    long ContentLength);

public sealed record ReceiptStoredFileContent(
    Stream Content,
    string ContentType,
    string BlobName);

public interface IReceiptFileStorage
{
    Task<ReceiptStoredFileInfo> UploadAsync(
        Stream content,
        string contentType,
        string blobName,
        CancellationToken ct = default);

    Task<ReceiptStoredFileContent?> OpenReadAsync(
        string containerName,
        string blobName,
        CancellationToken ct = default);
}
