using Erp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml.Linq;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Validates EU VAT numbers via the official EU VIES SOAP API.
/// Endpoint: https://ec.europa.eu/taxation_customs/vies/services/checkVatService
/// WSDL:     https://ec.europa.eu/taxation_customs/vies/checkVatService.wsdl
/// </summary>
public sealed class ViesService : IViesService
{
    private const string ViesEndpoint =
        "https://ec.europa.eu/taxation_customs/vies/services/checkVatService";

    private static readonly XNamespace Soap =
        "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace ViesNs =
        "urn:ec.europa.eu:taxud:vies:services:checkVat:types";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ViesService> _logger;

    public ViesService(IHttpClientFactory httpFactory, ILogger<ViesService> logger)
    {
        _httpFactory = httpFactory;
        _logger      = logger;
    }

    public async Task<ViesValidationResult> ValidateAsync(
        string countryCode, string vatNumber, CancellationToken ct = default)
    {
        countryCode = countryCode.Trim().ToUpperInvariant();
        vatNumber   = vatNumber.Trim().ToUpperInvariant()
                               .Replace(" ", "").Replace("-", "");

        // Build SOAP request
        var soapEnvelope = new XDocument(
            new XElement(Soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", Soap.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "v",    ViesNs.NamespaceName),
                new XElement(Soap + "Body",
                    new XElement(ViesNs + "checkVat",
                        new XElement(ViesNs + "countryCode", countryCode),
                        new XElement(ViesNs + "vatNumber",   vatNumber)))));

        var client  = _httpFactory.CreateClient("vies");
        var content = new StringContent(soapEnvelope.ToString(), Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", "\"\"");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(ViesEndpoint, content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VIES request failed for {Country}{Vat}", countryCode, vatNumber);
            return new ViesValidationResult(
                false, countryCode, vatNumber,
                null, null, null,
                $"VIES service unavailable: {ex.Message}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // VIES returns 500 with a SOAP Fault when the VAT number is invalid/not found
            var faultMsg = ExtractSoapFaultMessage(body);
            return new ViesValidationResult(
                false, countryCode, vatNumber,
                null, null, null, faultMsg);
        }

        return ParseViesResponse(body, countryCode, vatNumber);
    }

    // ── XML parsers ────────────────────────────────────────────────────────────

    private static ViesValidationResult ParseViesResponse(
        string xml, string countryCode, string vatNumber)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var resp = doc.Descendants(ViesNs + "checkVatResponse").FirstOrDefault()
                    ?? doc.Descendants("checkVatResponse").FirstOrDefault();

            if (resp is null)
                return new ViesValidationResult(false, countryCode, vatNumber,
                    null, null, null, "Unexpected VIES response format.");

            bool isValid = string.Equals(
                resp.Element(ViesNs + "valid")?.Value
                ?? resp.Element("valid")?.Value,
                "true", StringComparison.OrdinalIgnoreCase);

            return new ViesValidationResult(
                IsValid:     isValid,
                CountryCode: resp.Element(ViesNs + "countryCode")?.Value
                          ?? resp.Element("countryCode")?.Value
                          ?? countryCode,
                VatNumber:   resp.Element(ViesNs + "vatNumber")?.Value
                          ?? resp.Element("vatNumber")?.Value
                          ?? vatNumber,
                Name:        resp.Element(ViesNs + "name")?.Value
                          ?? resp.Element("name")?.Value,
                Address:     resp.Element(ViesNs + "address")?.Value
                          ?? resp.Element("address")?.Value,
                RequestDate: resp.Element(ViesNs + "requestDate")?.Value
                          ?? resp.Element("requestDate")?.Value,
                ErrorMessage: isValid ? null : "VAT number not found or invalid.");
        }
        catch (Exception ex)
        {
            return new ViesValidationResult(false, countryCode, vatNumber,
                null, null, null, $"Parse error: {ex.Message}");
        }
    }

    private static string ExtractSoapFaultMessage(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var fault = doc.Descendants("Fault").FirstOrDefault()
                     ?? doc.Descendants(Soap + "Fault").FirstOrDefault();
            var faultString = fault?.Element("faultstring")?.Value
                           ?? fault?.Element(Soap + "faultstring")?.Value;
            return faultString ?? "VIES service returned an error.";
        }
        catch
        {
            return "VIES service returned an error.";
        }
    }
}
