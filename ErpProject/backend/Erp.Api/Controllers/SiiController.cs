using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Services.Sii;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Erp.Api.Controllers;

/// <summary>
/// SII — Suministro Inmediato de Información (AEAT Spain).
/// Generates XML files for submission to Agencia Tributaria.
/// Required for companies with turnover > 6M€/year or voluntarily opted-in.
/// </summary>
[ApiController]
[Route("api/sii")]
[Authorize]
public class SiiController : ControllerBase
{
    private readonly SiiXmlGenerator         _generator;
    private readonly SiiSigningService       _signer;
    private readonly SiiSubmissionService    _submission;
    private readonly VerifactuXmlGenerator   _verifactuGen;
    private readonly VerifactuSubmissionService _verifactuSub;
    private readonly ITenantContext          _tenantContext;

    public SiiController(
        SiiXmlGenerator generator,
        SiiSigningService signer,
        SiiSubmissionService submission,
        VerifactuXmlGenerator verifactuGen,
        VerifactuSubmissionService verifactuSub,
        ITenantContext tenantContext)
    {
        _generator    = generator;
        _signer       = signer;
        _submission   = submission;
        _verifactuGen = verifactuGen;
        _verifactuSub = verifactuSub;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Generates XML for Libro de Facturas Emitidas (issued invoices).
    /// Returns downloadable XML file ready for AEAT submission.
    /// GET /api/sii/emitidas?year=2026&month=1
    /// </summary>
    [HttpGet("emitidas")]
    public async Task<IActionResult> GetFacturasEmitidas([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var xml = await _generator.GenerateFacturasEmitidasAsync(tenantId, year, month, ct);
        var bytes = Encoding.UTF8.GetBytes(xml);
        var fileName = $"SII_FacturasEmitidas_{year}_{month:D2}.xml";

        return File(bytes, "application/xml", fileName);
    }

    /// <summary>
    /// Generates XML for Libro de Facturas Recibidas (received/expense invoices).
    /// Returns downloadable XML file ready for AEAT submission.
    /// GET /api/sii/recibidas?year=2026&month=1
    /// </summary>
    [HttpGet("recibidas")]
    public async Task<IActionResult> GetFacturasRecibidas([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var xml = await _generator.GenerateFacturasRecibidasAsync(tenantId, year, month, ct);
        var bytes = Encoding.UTF8.GetBytes(xml);
        var fileName = $"SII_FacturasRecibidas_{year}_{month:D2}.xml";

        return File(bytes, "application/xml", fileName);
    }

    /// <summary>
    /// Returns a preview (JSON) of what would be submitted for the given period.
    /// Useful for review before generating the XML.
    /// </summary>
    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        // Generate both and return as base64 for preview
        var emitidas = await _generator.GenerateFacturasEmitidasAsync(tenantId, year, month, ct);
        var recibidas = await _generator.GenerateFacturasRecibidasAsync(tenantId, year, month, ct);

        return Ok(new
        {
            period = $"{year}-{month:D2}",
            facturasEmitidasXml = Convert.ToBase64String(Encoding.UTF8.GetBytes(emitidas)),
            facturasRecibidasXml = Convert.ToBase64String(Encoding.UTF8.GetBytes(recibidas)),
            message = "Decode from Base64 to review XML before submission"
        });
    }

    /// <summary>
    /// POST /api/sii/submit — signs and submits SII XML to AEAT telemáticamente.
    /// Requires Sii:CertPath and Sii:CertPass to be configured.
    /// Body: { "type": "emitidas"|"recibidas", "year": 2026, "month": 3 }
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SiiSubmitRequest req, CancellationToken ct)
    {
        if (req.Month < 1 || req.Month > 12)
            return BadRequest(new { error = "month must be 1–12" });

        if (!_signer.IsConfigured)
            return BadRequest(new
            {
                error = "SII signing certificate not configured.",
                hint  = "Set Sii:CertPath and Sii:CertPass in configuration."
            });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var invoiceType = req.Type?.ToLower() == "recibidas"
            ? SiiInvoiceType.Recibidas
            : SiiInvoiceType.Emitidas;

        // Generate XML
        var xml = invoiceType == SiiInvoiceType.Emitidas
            ? await _generator.GenerateFacturasEmitidasAsync(tenantId, req.Year, req.Month, ct)
            : await _generator.GenerateFacturasRecibidasAsync(tenantId, req.Year, req.Month, ct);

        // Sign with XAdES-BES
        var signedXml = _signer.Sign(xml);

        // Submit to AEAT
        var result = await _submission.SubmitAsync(signedXml, invoiceType, ct);

        return result.Success
            ? Ok(new { submitted = true, estado = result.Estado, period = $"{req.Year}-{req.Month:D2}" })
            : StatusCode(502, new { submitted = false, error = result.RawResponse });
    }

    // GET /api/sii/verifactu?year=2026&month=3
    [HttpGet("verifactu")]
    public async Task<IActionResult> GetVerifactuXml(
        [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { error = "month debe estar entre 1 y 12" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var xml   = await _verifactuGen.GenerateRegistroAsync(tenantId, year, month, ct);
        var bytes = System.Text.Encoding.UTF8.GetBytes(xml);
        return File(bytes, "application/xml", $"Verifactu_{year}_{month:D2}.xml");
    }

    // POST /api/sii/verifactu/submit
    [HttpPost("verifactu/submit")]
    public async Task<IActionResult> SubmitVerifactu(
        [FromBody] VerifactuSubmitRequest req, CancellationToken ct)
    {
        if (req.Month < 1 || req.Month > 12)
            return BadRequest(new { error = "month debe estar entre 1 y 12" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var xml    = await _verifactuGen.GenerateRegistroAsync(tenantId, req.Year, req.Month, ct);
        var result = await _verifactuSub.SubmitAsync(xml, req.UseProd, ct);

        return result.Success
            ? Ok(new { submitted = true, estadoEnvio = result.EstadoEnvio, period = $"{req.Year}-{req.Month:D2}" })
            : StatusCode(502, new { submitted = false, estadoEnvio = result.EstadoEnvio, error = result.RawResponse });
    }
}

public record SiiSubmitRequest(string? Type, int Year, int Month);
public record VerifactuSubmitRequest(int Year, int Month, bool UseProd = false);
