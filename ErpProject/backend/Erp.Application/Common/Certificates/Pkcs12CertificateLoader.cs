using System.Security.Cryptography.X509Certificates;

namespace Erp.Application.Common.Certificates;

/// <summary>Carga certificados PKCS#12 sin API obsoleta (SYSLIB0057).</summary>
public static class Pkcs12CertificateLoader
{
    public static X509Certificate2 Load(string path, string? password)
    {
        var bytes = File.ReadAllBytes(path);
        return X509CertificateLoader.LoadPkcs12(
            bytes,
            password ?? string.Empty,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }
}
