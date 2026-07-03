using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>Envío FacturaE a FACe (B2G). SOAP real opcional vía Face:SendEnabled.</summary>
public sealed class FaceSubmissionService : IFaceSubmissionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FaceSubmissionService> _logger;
    private readonly string? _endpoint;
    private readonly bool _sendEnabled;

    public FaceSubmissionService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<FaceSubmissionService> logger)
    {
        _endpoint = config["Face:Endpoint"];
        _sendEnabled = config.GetValue("Face:SendEnabled", false);
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_endpoint);

    public async Task<FaceSubmissionResult> SubmitAsync(
        byte[] signedXml, string fileName, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new FaceSubmissionResult(
                false,
                "FACe no configurado. Defina Face:Endpoint en configuración.");
        }

        if (signedXml.Length == 0)
            return new FaceSubmissionResult(false, "XML firmado vacío.");

        var payload = Convert.ToBase64String(signedXml);
        var soap = BuildFaceSoapEnvelope(fileName, payload);

        var soapValidation = FaceSoapStructureValidator.Validate(soap);
        if (!soapValidation.IsValid)
        {
            return new FaceSubmissionResult(
                false,
                "FACe: SOAP inválido — " + string.Join("; ", soapValidation.Errors),
                ReferenceId: fileName);
        }

        if (!_sendEnabled)
        {
            return new FaceSubmissionResult(
                false,
                $"FACe: SOAP preparado ({soap.Length} chars) para {_endpoint}. " +
                "Active Face:SendEnabled=true para envío HTTP.",
                ReferenceId: fileName);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Face");
            using var content = new StringContent(soap, System.Text.Encoding.UTF8, "text/xml");
            using var response = await client.PostAsync(_endpoint, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "FACe: envío OK {FileName} ? {StatusCode}", fileName, (int)response.StatusCode);
                return new FaceSubmissionResult(
                    true,
                    $"FACe: enviado correctamente (HTTP {(int)response.StatusCode}).",
                    ReferenceId: fileName,
                    HttpStatusCode: (int)response.StatusCode,
                    ResponseBody: Truncate(body, 2000));
            }

            _logger.LogWarning(
                "FACe: error HTTP {StatusCode} para {FileName}: {Body}",
                (int)response.StatusCode, fileName, Truncate(body, 500));
            return new FaceSubmissionResult(
                false,
                $"FACe: error HTTP {(int)response.StatusCode}.",
                ReferenceId: fileName,
                HttpStatusCode: (int)response.StatusCode,
                ResponseBody: Truncate(body, 2000));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FACe: fallo de red enviando {FileName}", fileName);
            return new FaceSubmissionResult(false, $"FACe: error de red — {ex.Message}", ReferenceId: fileName);
        }
    }

    private static string BuildFaceSoapEnvelope(string fileName, string base64Content) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
          <soapenv:Header/>
          <soapenv:Body>
            <face:SubmitInvoice xmlns:face="https://face.gob.es/schema">
              <face:FileName>{System.Security.SecurityElement.Escape(fileName)}</face:FileName>
              <face:Content>{base64Content}</face:Content>
            </face:SubmitInvoice>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
