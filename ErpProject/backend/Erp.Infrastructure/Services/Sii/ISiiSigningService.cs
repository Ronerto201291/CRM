namespace Erp.Infrastructure.Services.Sii;

/// <summary>Firma XAdES-BES de XML SII. Implementación real o mock en tests.</summary>
public interface ISiiSigningService
{
    bool IsConfigured { get; }
    string Sign(string xmlContent);
}
