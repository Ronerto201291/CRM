using Erp.Modules.Accounting.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Erp.Modules.Accounting.Application.Features.FinancialStatements;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/financial-statements")]
[Authorize]
public class FinancialStatementsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinancialStatementsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("cash-flow")]
    public async Task<IActionResult> GenerateCashFlow([FromBody] CashFlowRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateCashFlowStatementQuery(request.Year, request.Month), ct);
        return Ok(result);
    }

    [HttpPost("equity")]
    public async Task<IActionResult> GenerateEquityStatement([FromBody] EquityRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateEquityStatementQuery(request.Year), ct);
        return Ok(result);
    }

    [HttpPost("income-statement")]
    public async Task<IActionResult> GenerateIncomeStatement([FromBody] PeriodRangeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProfitAndLossQuery
        {
            FechaInicio = request.From,
            FechaFin = request.To
        }, ct);
        return Ok(result);
    }

    [HttpPost("balance-sheet")]
    public async Task<IActionResult> GenerateBalanceSheet([FromBody] BalanceSheetRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBalanceSheetQuery { FechaCorte = request.AsOf }, ct);
        return Ok(result);
    }
}

public record CashFlowRequest(int Year, int Month);
public record EquityRequest(int Year);
public record PeriodRangeRequest(DateTime From, DateTime To);
public record BalanceSheetRequest(DateTime AsOf);