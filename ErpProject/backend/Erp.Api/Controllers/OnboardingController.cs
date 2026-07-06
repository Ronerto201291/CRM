using Erp.Modules.Accounting.Application.Features.Onboarding;
using Erp.Modules.Crm.Application.Features.Onboarding;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/onboarding"), Authorize]
public class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;
    public OnboardingController(IMediator mediator) => _mediator = mediator;

    [HttpGet("sectors")]
    public async Task<IActionResult> GetSectors(CancellationToken ct)
        => Ok(await _mediator.Send(new GetOnboardingSectorsQuery(), ct));

    [HttpPost("sectors/{sectorId}/apply")]
    public async Task<IActionResult> ApplySector(string sectorId, CancellationToken ct)
    {
        try
        {
            var added = await _mediator.Send(new ApplyOnboardingSectorCommand(sectorId), ct);
            return Ok(new { accountsAdded = added, message = $"Plan contable ampliado ({added} cuentas)." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("import-clients")]
    public async Task<IActionResult> ImportClients([FromBody] ImportClientsRequest? body, CancellationToken ct)
    {
        if (body is null || (string.IsNullOrWhiteSpace(body.CsvContent) && body.FileBase64 is null))
            return BadRequest(new { error = "CSV o archivo Excel requerido." });

        byte[]? fileBytes = null;
        if (!string.IsNullOrWhiteSpace(body.FileBase64))
        {
            try { fileBytes = Convert.FromBase64String(body.FileBase64); }
            catch (FormatException) { return BadRequest(new { error = "Archivo base64 inválido." }); }
        }

        var result = await _mediator.Send(new ImportOnboardingClientsCommand(
            body.CsvContent, fileBytes, body.FileName), ct);
        return Ok(result);
    }
}

public record ImportClientsRequest(string? CsvContent = null, string? FileBase64 = null, string? FileName = null);
