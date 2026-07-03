using Erp.Modules.Billing.Application.Features.Billing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Billing.Api.Controllers;

/// <summary>
/// Endpoints para generación de FacturaE 3.2.2 (Ley 18/2022 Crea y Crece).
/// </summary>
[ApiController]
[Route("api/v1/billing/facturae")]
[Authorize]
public class FacturaEController : ControllerBase
{
    private readonly IMediator _mediator;

    public FacturaEController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{invoiceId:guid}")]
    public async Task<IActionResult> GenerateFacturaE(Guid invoiceId, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GenerateFacturaEQuery(invoiceId), ct);
            return File(result.XmlBytes, "application/xml", result.FileName);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{invoiceId:guid}/signed")]
    public async Task<IActionResult> GenerateSignedFacturaE(Guid invoiceId, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GenerateSignedFacturaEQuery(invoiceId), ct);
            return File(result.XmlBytes, "application/xml", result.FileName);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{invoiceId:guid}/validate")]
    public async Task<IActionResult> ValidateFacturaE(Guid invoiceId, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new ValidateFacturaEQuery(invoiceId), ct);
            return Ok(new
            {
                valid = result.Valid,
                fileName = result.FileName,
                errors = result.Errors,
                warnings = result.Warnings
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{invoiceId:guid}/submit-face")]
    public async Task<IActionResult> SubmitToFace(Guid invoiceId, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new SubmitFacturaEFaceCommand(invoiceId), ct);
            return result.Success ? Ok(result) : StatusCode(501, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
