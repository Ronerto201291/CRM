namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Abstracción para almacenamiento de archivos con soporte de cifrado en reposo.
/// Implementada por MinioFileStorageService (S3-compatible, RL-4 RGPD).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Sube un stream al almacenamiento y devuelve la clave (object key) del archivo.
    /// </summary>
    Task<string> UploadAsync(
        string bucketName,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Descarga un archivo y devuelve su stream.
    /// </summary>
    Task<Stream> DownloadAsync(string bucketName, string objectKey, CancellationToken ct = default);

    /// <summary>
    /// Genera una URL pre-firmada con tiempo de expiración (acceso temporal seguro).
    /// </summary>
    Task<string> GetSignedUrlAsync(string bucketName, string objectKey, TimeSpan expiry);

    /// <summary>
    /// Elimina un objeto del almacenamiento.
    /// </summary>
    Task DeleteAsync(string bucketName, string objectKey, CancellationToken ct = default);
}
