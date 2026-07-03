using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

/// <summary>Almacenamiento en memoria para tests — sin depender de MinIO real.</summary>
public sealed class FakeFileStorageService : IFileStorageService
{
    public Dictionary<string, byte[]> Objects { get; } = new();
    public List<string> DeletedKeys { get; } = new();

    public Task<string> UploadAsync(string bucketName, string objectKey, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        Objects[objectKey] = ms.ToArray();
        return Task.FromResult(objectKey);
    }

    public Task<Stream> DownloadAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        if (!Objects.TryGetValue(objectKey, out var bytes))
            throw new FileNotFoundException(objectKey);
        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task<string> GetSignedUrlAsync(string bucketName, string objectKey, TimeSpan expiry)
        => Task.FromResult($"https://fake-storage.local/{bucketName}/{objectKey}?expires={expiry.TotalSeconds}");

    public Task DeleteAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        Objects.Remove(objectKey);
        DeletedKeys.Add(objectKey);
        return Task.CompletedTask;
    }
}
