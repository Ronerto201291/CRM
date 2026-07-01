using Erp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace Erp.Infrastructure.Services.Storage;

/// <summary>
/// Almacenamiento de archivos sobre MinIO (S3-compatible).
/// Soporta cifrado en reposo configurando SSE en el servidor MinIO/S3.
///
/// Config keys:
///   Storage:Endpoint    — e.g. "minio.empresa.com:9000"
///   Storage:AccessKey   — MinIO access key (env var: Storage__AccessKey)
///   Storage:SecretKey   — MinIO secret key (env var: Storage__SecretKey)
///   Storage:UseSSL      — true/false (default: true en producción)
///   Storage:BucketName  — nombre del bucket (default: "erp-expenses")
/// </summary>
public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minio;
    private readonly string _defaultBucket;
    private readonly ILogger<MinioFileStorageService> _logger;

    public MinioFileStorageService(IConfiguration config, ILogger<MinioFileStorageService> logger)
    {
        _logger = logger;
        _defaultBucket = config["Storage:BucketName"] ?? "erp-expenses";

        var endpoint  = config["Storage:Endpoint"]  ?? "localhost:9000";
        var accessKey = config["Storage:AccessKey"]  ?? "minioadmin";
        var secretKey = config["Storage:SecretKey"]  ?? "minioadmin";
        var useSSL    = bool.Parse(config["Storage:UseSSL"] ?? "false");

        _minio = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSSL)
            .Build();
    }

    public async Task<string> UploadAsync(
        string bucketName,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(bucketName, ct);

        var args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        await _minio.PutObjectAsync(args, ct);
        _logger.LogDebug("Uploaded {Key} to bucket {Bucket}", objectKey, bucketName);
        return objectKey;
    }

    public async Task<Stream> DownloadAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        var ms = new MemoryStream();

        var args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithCallbackStream(async (stream, innerCt) =>
            {
                await stream.CopyToAsync(ms, innerCt);
            });

        await _minio.GetObjectAsync(args, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task<string> GetSignedUrlAsync(string bucketName, string objectKey, TimeSpan expiry)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithExpiry((int)expiry.TotalSeconds);

        return await _minio.PresignedGetObjectAsync(args);
    }

    public async Task DeleteAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey);

        await _minio.RemoveObjectAsync(args, ct);
        _logger.LogDebug("Deleted {Key} from bucket {Bucket}", objectKey, bucketName);
    }

    private async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct)
    {
        var exists = await _minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName), ct);

        if (!exists)
        {
            await _minio.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucketName), ct);
            _logger.LogInformation("Created bucket {Bucket}", bucketName);
        }
    }
}
