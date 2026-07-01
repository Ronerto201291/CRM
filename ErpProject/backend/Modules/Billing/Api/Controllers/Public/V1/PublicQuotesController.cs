using Asp.Versioning;
using Erp.Modules.Billing.Application.Features.Quotes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Billing.Api.Controllers.Public.V1;

/// <summary>
/// Portal público del cliente para presupuestos.
/// No requiere autenticación — acceso por AcceptanceToken único.
/// Route: /api/v1/public/quotes/{token}
///
/// El token se incluye en el email enviado al cliente.
/// Genera evidencia de aceptación (IP + UserAgent + timestamp) en QuoteStatusHistory.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/quotes")]
[AllowAnonymous]
public class PublicQuotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicQuotesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Devuelve los datos públicos del presupuesto (sin datos internos).
    /// El cliente puede ver esto sin estar logueado.
    /// </summary>
    [HttpGet("{token}")]
    public async Task<IActionResult> GetByToken(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
            return BadRequest(new { error = "Token inválido." });

        var result = await _mediator.Send(new GetQuoteByTokenQuery { Token = token }, ct);

        if (result is null)
            return NotFound(new { error = "Presupuesto no encontrado o token inválido." });

        return Ok(result);
    }

    /// <summary>
    /// El cliente acepta el presupuesto desde el portal.
    /// Registra evidencia: IP, User-Agent, timestamp.
    /// Solo posible si el presupuesto está en estado Sent.
    /// </summary>
    [HttpPost("{token}/accept")]
    public async Task<IActionResult> Accept(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
            return BadRequest(new { error = "Token inválido." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        try
        {
            var ok = await _mediator.Send(new AcceptQuoteCommand
            {
                AcceptanceToken = token,
                IpAddress = ip,
                UserAgent = userAgent,
                AcceptedFromEmail = null  // no disponible desde portal sin login
            }, ct);

            if (!ok) return NotFound(new { error = "Presupuesto no encontrado o token inválido." });

            return Ok(new
            {
                message = "Presupuesto aceptado correctamente. Nos pondremos en contacto con usted en breve.",
                accepted = true
            });
        }
        catch (InvalidOperationException ex)
        {
            // El presupuesto puede haber expirado o ya estar en otro estado
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// El cliente rechaza el presupuesto desde el portal.
    /// Registra evidencia: IP, User-Agent, motivo.
    /// </summary>
    [HttpPost("{token}/reject")]
    public async Task<IActionResult> Reject(
        string token,
        [FromBody] PublicRejectRequest? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
            return BadRequest(new { error = "Token inválido." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        try
        {
            var ok = await _mediator.Send(new RejectQuoteCommand
            {
                AcceptanceToken = token,
                Reason = body?.Reason,
                IpAddress = ip,
                UserAgent = userAgent
            }, ct);

            if (!ok) return NotFound(new { error = "Presupuesto no encontrado o token inválido." });

            return Ok(new
            {
                message = "Hemos registrado su respuesta. Gracias por comunicarnos su decisión.",
                rejected = true
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record PublicRejectRequest(string? Reason);
