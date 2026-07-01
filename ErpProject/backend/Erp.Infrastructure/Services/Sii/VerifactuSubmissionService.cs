using Erp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml;

namespace Erp.Infrastructure.Services.Sii;

/// <summary>
/// Envía el XML VERI*FACTU al registro TIKE de la AEAT (RD 1007/2023).
///
/// Endpoints AEAT:
///   PRE:  https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SuministroFacturas
///   PROD: https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SuministroFacturas
///
/// Autenticación: certificado digital FNMT + mTLS (igual que SII).
/// El cliente HTTP "sii-aeat" ya tiene configurado mTLS y retry en DependencyInjection.cs.
///
/// IMPORTANTE: La integración con AEAT requiere homologación previa en el entorno PRE.
/// Verificar contra la especificación actualizada en sede.agenciatributaria.gob.es
/// antes de activar en producción.
/// </summary>
public class VerifactuSubmissionService : IVerifactuSubmissionService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<VerifactuSubmissionService> _logger;

    // Endpoints AEAT TIKE
    private const string EndpointPre  = "https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SuministroFacturas";
    private const string EndpointProd = "https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SuministroFacturas";

    public VerifactuSubmissionService(
        IHttpClientFactory httpFactory,
        ILogger<VerifactuSubmissionService> logger)
    {
        _httpFactory = httpFactory;
        _logger      = logger;
    }

    /// <summary>
    /// Envía un XML de registro VERI*FACTU al endpoint TIKE.
    /// </summary>
    /// <param name="verifactuXml">XML generado por VerifactuXmlGenerator.</param>
    /// <param name="useProd">Si true usa el endpoint de producción, si false el PRE.</param>
    /// <returns>EstadoEnvio de la respuesta AEAT ("Correcto", "AceptadoConErrores", "Incorrecto").</returns>
    public async Task<VerifactuSubmissionResult> SubmitAsync(
        string verifactuXml,
        bool useProd = false,
        CancellationToken ct = default)
    {
        var endpoint = useProd ? EndpointProd : EndpointPre;
        var client   = _httpFactory.CreateClient("sii-aeat");

        // VERI*FACTU usa HTTP POST con Content-Type: application/xml (no SOAP)
        using var content = new StringContent(verifactuXml, Encoding.UTF8, "application/xml");

        _logger.LogInformation("Submitting VERI*FACTU XML to {Endpoint}", endpoint);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(endpoint, content, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error submitting to TIKE endpoint");
            return new VerifactuSubmissionResult(false, "NetworkError", ex.Message);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("TIKE response HTTP {Status}: {Body}", response.StatusCode, body);

        if (!response.IsSuccessStatusCode)
        {
            return new VerifactuSubmissionResult(false,
                $"HTTP{(int)response.StatusCode}", body);
        }

        // Parsear respuesta AEAT — el elemento EstadoEnvio indica el resultado
        var estado = ParseEstadoEnvio(body);
        var ok     = estado == "Correcto";

        if (!ok)
            _logger.LogWarning("TIKE returned EstadoEnvio={Estado}: {Body}", estado, body);
        else
            _logger.LogInformation("TIKE submission accepted: EstadoEnvio=Correcto");

        return new VerifactuSubmissionResult(ok, estado, body);
    }

    /// <inheritdoc />
    public Task<VerifactuSubmitResult> SubmitSingleAsync(
        string verifactuXml,
        bool useProd = false,
        CancellationToken ct = default)
    {
        // Delegate to the existing SubmitAsync and map result types
        return SubmitAsync(verifactuXml, useProd, ct)
            .ContinueWith(t => new VerifactuSubmitResult(
                t.Result.Success,
                t.Result.EstadoEnvio,
                t.Result.RawResponse), ct);
    }

    private static string ParseEstadoEnvio(string xml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var mgr = new XmlNamespaceManager(doc.NameTable);
            mgr.AddNamespace("tike", "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd");
            var node = doc.SelectSingleNode("//tike:EstadoEnvio", mgr)
                    ?? doc.SelectSingleNode("//*[local-name()='EstadoEnvio']");
            return node?.InnerText ?? "Unknown";
        }
        catch
        {
            return "ParseError";
        }
    }
}

/// <summary>Resultado del envío al registro TIKE de la AEAT.</summary>
public record VerifactuSubmissionResult(
    bool   Success,
    string EstadoEnvio,  // "Correcto", "AceptadoConErrores", "Incorrecto", "NetworkError"
    string RawResponse);
