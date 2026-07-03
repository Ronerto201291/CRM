using System.Text;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Services.Sii;

/// <summary>
/// Submits signed SII XML to AEAT via SOAP WS.
/// Uses mutual TLS (client certificate) for authentication.
/// Retry (x3 exponential) and circuit breaker are applied via the "sii-aeat" named
/// HttpClient registered in DependencyInjection.cs with Microsoft.Extensions.Http.Resilience.
///
/// Endpoints (from config Sii:Environment: "test" | "prod"):
///   test — https://www1.agenciatributaria.gob.es/wlpl/SSII-FACT/ws/fe/SiiFactFEV1SOAP
///   prod — https://www2.agenciatributaria.gob.es/wlpl/SSII-FACT/ws/fe/SiiFactFEV1SOAP
/// </summary>
public class SiiSubmissionService
{
    private readonly ILogger<SiiSubmissionService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _endpoint;
    private readonly bool _sendEnabled;

    private const string TestEndpoint = "https://www1.agenciatributaria.gob.es/wlpl/SSII-FACT/ws/fe/SiiFactFEV1SOAP";
    private const string ProdEndpoint = "https://www2.agenciatributaria.gob.es/wlpl/SSII-FACT/ws/fe/SiiFactFEV1SOAP";

    public SiiSubmissionService(IConfiguration config, ILogger<SiiSubmissionService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger             = logger;
        _httpClientFactory  = httpClientFactory;
        _endpoint           = (config["Sii:Environment"] ?? "test") == "prod" ? ProdEndpoint : TestEndpoint;
        _sendEnabled        = config.GetValue("Sii:SendEnabled", false);
    }

    /// <summary>
    /// Submits signed SII XML (FacturasEmitidas or FacturasRecibidas) to AEAT.
    /// Returns the AEAT SOAP response XML.
    /// </summary>
    public async Task<SiiSubmissionResult> SubmitAsync(
        string signedXml,
        SiiInvoiceType invoiceType,
        CancellationToken ct = default)
    {
        var soapAction = invoiceType == SiiInvoiceType.Emitidas
            ? "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/ssii/fact/ws/SuministroLR.xsd#SuministroLRFacturasEmitidas"
            : "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/ssii/fact/ws/SuministroLR.xsd#SuministroLRFacturasRecibidas";

        var soapBody  = invoiceType == SiiInvoiceType.Emitidas
            ? "SuministroLRFacturasEmitidas"
            : "SuministroLRFacturasRecibidas";

        var envelope = BuildSoapEnvelope(signedXml, soapBody);

        if (!_sendEnabled)
        {
            _logger.LogInformation(
                "SII {Type}: envío deshabilitado (Sii:SendEnabled=false). SOAP preparado ({Len} chars) para {Endpoint}.",
                invoiceType, envelope.Length, _endpoint);
            return new SiiSubmissionResult(
                false,
                $"SII: SOAP preparado para {_endpoint}. Active Sii:SendEnabled=true para envío HTTP.",
                null);
        }

        try
        {
            using var client   = _httpClientFactory.CreateClient("sii-aeat");
            using var request  = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", $"\"{soapAction}\"");

            _logger.LogInformation("Submitting SII {Type} to AEAT ({Endpoint})", invoiceType, _endpoint);

            using var response = await client.SendAsync(request, ct);
            var responseXml = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("AEAT returned HTTP {Code}: {Body}", response.StatusCode, responseXml);
                return new SiiSubmissionResult(false, responseXml, null);
            }

            var estado = ParseEstado(responseXml);
            _logger.LogInformation("AEAT response estado: {Estado}", estado);
            return new SiiSubmissionResult(true, responseXml, estado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting SII to AEAT");
            return new SiiSubmissionResult(false, ex.Message, null);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildSoapEnvelope(string signedXml, string _)
    {
        var bodyContent = signedXml.Trim();
        if (bodyContent.StartsWith("<?xml", StringComparison.Ordinal))
        {
            var idx = bodyContent.IndexOf("?>", StringComparison.Ordinal);
            if (idx >= 0) bodyContent = bodyContent[(idx + 2)..].Trim();
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
              <soapenv:Header/>
              <soapenv:Body>
                {bodyContent}
              </soapenv:Body>
            </soapenv:Envelope>
            """;
    }

    private static string? ParseEstado(string soapResponse)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(soapResponse);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("sii", "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/ssii/fact/ws/SuministroInformacion.xsd");
            return doc.SelectSingleNode("//sii:EstadoEnvio", ns)?.InnerText
                ?? doc.SelectSingleNode("//*[local-name()='EstadoEnvio']")?.InnerText;
        }
        catch { return null; }
    }
}

public enum SiiInvoiceType { Emitidas, Recibidas }

public record SiiSubmissionResult(bool Success, string RawResponse, string? Estado);
