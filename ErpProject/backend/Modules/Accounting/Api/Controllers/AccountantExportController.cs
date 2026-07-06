using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Features.AccountantExport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/accountant-export")]
[Authorize]
[RequiredModule("Accounting")]
[RequirePermission("Accounting", "Export")]
public class AccountantExportController(IMediator mediator) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await mediator.Send(new GetAccountantExportSettingsQuery(), ct));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateAccountantExportSettingsRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateAccountantExportSettingsCommand(body.AccountantEmail, body.Frequency), ct));

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportAccountantPackageRequest? body, CancellationToken ct)
    {
        var result = await mediator.Send(new ExportAccountantPackageCommand(
            body?.Year, body?.Month, body?.Quarter, body?.SendEmail ?? false), ct);
        return File(result.ZipContent, "application/zip", result.FileName);
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendNow([FromBody] ExportAccountantPackageRequest? body, CancellationToken ct)
    {
        var result = await mediator.Send(new ExportAccountantPackageCommand(
            body?.Year, body?.Month, body?.Quarter, SendEmail: true), ct);
        return Ok(new { result.EmailSent, result.Message, result.FileName });
    }
}

public record UpdateAccountantExportSettingsRequest(string? AccountantEmail, string Frequency);
public record ExportAccountantPackageRequest(int? Year, int? Month, int? Quarter, bool SendEmail = false);
