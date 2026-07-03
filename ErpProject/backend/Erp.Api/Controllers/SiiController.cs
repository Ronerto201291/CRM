using Erp.Infrastructure.Features.Sii;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

/// <summary>
/// SII — Suministro Inmediato de Información (AEAT Spain).
/// </summary>
[ApiController]
[Route("api/sii")]
[Authorize]
public class SiiController : ControllerBase
{
    private readonly IMediator _mediator;

    public SiiController(IMediator mediator) => _mediator = mediator;

    [HttpGet("emitidas")]
    public async Task<IActionResult> GetFacturasEmitidas([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        var file = await _mediator.Send(new GetSiiEmitidasXmlQuery(year, month), ct);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpGet("recibidas")]
    public async Task<IActionResult> GetFacturasRecibidas([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        var file = await _mediator.Send(new GetSiiRecibidasXmlQuery(year, month), ct);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpGet("validate")]
    public async Task<IActionResult> Validate(
        [FromQuery] int year, [FromQuery] int month, [FromQuery] string? type, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        var result = await _mediator.Send(new ValidateSiiXmlQuery(year, month, type), ct);
        return Ok(new
        {
            period = result.Period,
            type = result.Type,
            valid = result.Valid,
            errors = result.Errors,
            warnings = result.Warnings,
            signerConfigured = result.SignerConfigured
        });
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { error = "Month must be between 1 and 12" });

        var result = await _mediator.Send(new PreviewSiiQuery(year, month), ct);
        return Ok(new
        {
            period = result.Period,
            facturasEmitidasXml = result.FacturasEmitidasXml,
            facturasRecibidasXml = result.FacturasRecibidasXml,
            message = result.Message
        });
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SiiSubmitRequest req, CancellationToken ct)
    {
        if (req.Month < 1 || req.Month > 12)
            return BadRequest(new { error = "month must be 1–12" });

        try
        {
            var result = await _mediator.Send(new SubmitSiiCommand(req.Type, req.Year, req.Month), ct);
            return result.Success
                ? Ok(new { submitted = true, estado = result.Estado, period = result.Period })
                : StatusCode(502, new { submitted = false, error = result.Error });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                error = "SII signing certificate not configured.",
                hint = "Set Sii:CertPath and Sii:CertPass in configuration."
            });
        }
    }

    [HttpGet("verifactu")]
    public async Task<IActionResult> GetVerifactuXml([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new { error = "month debe estar entre 1 y 12" });

        var file = await _mediator.Send(new GetVerifactuXmlQuery(year, month), ct);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpPost("verifactu/submit")]
    public async Task<IActionResult> SubmitVerifactu([FromBody] VerifactuSubmitRequest req, CancellationToken ct)
    {
        if (req.Month < 1 || req.Month > 12)
            return BadRequest(new { error = "month debe estar entre 1 y 12" });

        var result = await _mediator.Send(new SubmitVerifactuCommand(req.Year, req.Month, req.UseProd), ct);
        return result.Success
            ? Ok(new { submitted = true, estadoEnvio = result.EstadoEnvio, period = result.Period })
            : StatusCode(502, new { submitted = false, estadoEnvio = result.EstadoEnvio, error = result.Error });
    }
}

public record SiiSubmitRequest(string? Type, int Year, int Month);
public record VerifactuSubmitRequest(int Year, int Month, bool UseProd = false);
