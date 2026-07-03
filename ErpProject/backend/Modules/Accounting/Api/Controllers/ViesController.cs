using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Vat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/vies")]
[Authorize]
public class ViesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenant;

    public ViesController(IMediator mediator, ITenantContext tenant)
    {
        _mediator = mediator;
        _tenant = tenant;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateVat([FromBody] ValidateVatApiRequest request, CancellationToken ct)
    {
        var vatNumber = !string.IsNullOrWhiteSpace(request.VatNumber)
            ? request.VatNumber
            : $"{request.CountryCode}{request.VatNumberOnly}";

        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var result = await _mediator.Send(new ValidateViesCommand
        {
            VatNumber = vatNumber.Trim().ToUpperInvariant(),
            CompanyId = companyId
        }, ct);

        return Ok(new
        {
            validationId = result.ValidationId,
            countryCode = vatNumber.Length >= 2 ? vatNumber[..2] : request.CountryCode,
            vatNumber = result.VatNumber,
            isValid = result.IsValid,
            companyName = result.CompanyName,
            address = result.Address,
            validationStatus = result.Status,
            reason = result.Reason,
            requestedAt = result.RequestedAt
        });
    }
}

public class ValidateVatApiRequest
{
    public string CountryCode { get; set; } = string.Empty;
    public string VatNumberOnly { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
}
