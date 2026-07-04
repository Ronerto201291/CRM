using Asp.Versioning;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Billing.Api.Controllers.Public.V1;

/// <summary>
/// Portal público del cliente para ver una factura (ADR-0018 #39, mismo patrón que
/// PublicQuotesController). No requiere autenticación — acceso por PublicViewToken único.
/// Route: /api/v1/public/invoice-view/{token}
///
/// No confundir con PublicInvoicesController (api/v1/public/invoices), que es un
/// endpoint de integración protegido por X-Api-Key para todo el ledger del tenant, no
/// un visor de una factura concreta para el cliente que la recibió.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/invoice-view")]
[AllowAnonymous]
public class PublicInvoiceViewController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicInvoiceViewController(IMediator mediator) => _mediator = mediator;

    /// <summary>Devuelve los datos públicos de la factura (sin datos internos).</summary>
    [HttpGet("{token}")]
    public async Task<IActionResult> GetByToken(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
            return BadRequest(new { error = "Token inválido." });

        var result = await _mediator.Send(new GetInvoiceByTokenQuery { Token = token }, ct);

        if (result is null)
            return NotFound(new { error = "Factura no encontrada o token inválido." });

        return Ok(result);
    }

    /// <summary>
    /// Crea una sesión de pago Stripe puntual por el importe exacto de la factura y
    /// devuelve la URL de checkout hospedada por Stripe (ADR-0018 #39).
    /// </summary>
    [HttpPost("{token}/checkout")]
    public async Task<IActionResult> CreateCheckout(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
            return BadRequest(new { error = "Token inválido." });

        try
        {
            var checkoutUrl = await _mediator.Send(new CreateInvoiceCheckoutSessionCommand { Token = token }, ct);
            return Ok(new { checkoutUrl });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
