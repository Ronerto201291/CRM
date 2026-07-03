using Erp.Modules.Accounting.Application.Features.FinancialStatements;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/financial-statements")]
[Authorize]
public class FinancialStatementsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinancialStatementsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("cash-flow")]
    public async Task<IActionResult> GenerateCashFlow([FromBody] GenerateCashFlowRequest request, CancellationToken ct)
    {
        var year = request.FiscalYear > 0 ? request.FiscalYear : DateTime.UtcNow.Year;
        var result = await _mediator.Send(new GenerateCashFlowCommand(year), ct);
        return Ok(result);
    }

    [HttpPost("equity")]
    public async Task<IActionResult> GenerateEquityStatement([FromBody] GenerateCashFlowRequest request, CancellationToken ct)
    {
        var year = request.FiscalYear > 0 ? request.FiscalYear : DateTime.UtcNow.Year;
        var result = await _mediator.Send(new GenerateEquityStatementCommand(year), ct);
        return Ok(new
        {
            id = result.Id,
            beginningCapital = result.BeginningCapital,
            netIncome = result.NetIncome,
            dividendsPaid = result.DividendsPaid,
            otherChanges = result.OtherChanges,
            endingCapital = result.EndingCapital,
            status = result.Status
        });
    }
}

public class GenerateCashFlowRequest
{
    public int FiscalYear { get; set; }
}
