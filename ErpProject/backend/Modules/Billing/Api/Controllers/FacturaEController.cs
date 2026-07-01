using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Billing.Api.Controllers;

/// <summary>
/// Endpoints para generación de FacturaE 3.2.2 (Ley 18/2022 Crea y Crece).
/// Todos los endpoints requieren autenticación JWT y que la factura esté bloqueada (IsLocked=true).
/// </summary>
[ApiController]
[Route("api/v1/billing/facturae")]
[Authorize]
public class FacturaEController : ControllerBase
{
    private readonly IFacturaEService _facturaE;
    private readonly ITenantContext   _tenant;

    public FacturaEController(IFacturaEService facturaE, ITenantContext tenant)
    {
        _facturaE = facturaE;
        _tenant   = tenant;
    }

    /// <summary>
    /// GET /api/v1/billing/facturae/{invoiceId}
    /// Genera y descarga el XML FacturaE 3.2.2 de la factura indicada.
    /// La factura debe estar bloqueada (IsLocked=true) — Ley 11/2021 Antifraude.
    /// El XML generado cumple el esquema oficial Facturae32.xsd.
    /// </summary>
    [HttpGet("{invoiceId:guid}")]
    public async Task<IActionResult> GenerateFacturaE(Guid invoiceId, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant no resuelto.");

        try
        {
            var (xmlBytes, fileName) = await _facturaE.GenerateAsync(invoiceId, tenantId, ct);
            return File(xmlBytes, "application/xml", fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Factura no bloqueada u otro error de negocio
            return BadRequest(new { error = ex.Message });
        }
    }
}
