using Erp.Application.Common.Attributes;
using Erp.Modules.Treasury.Application.Features.Currencies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/treasury/currencies")]
[Authorize]
[RequiredModule("Treasury")]
public class CurrenciesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CurrenciesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Currency.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCurrenciesQuery(), ct));

    [HttpPost]
    [RequirePermission(Permissions.Currency.Create)]
    public async Task<IActionResult> Create([FromBody] CreateCurrencyDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CreateCurrencyCommand(dto.Code, dto.Name, dto.ExchangeRate), ct);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Currency.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCurrencyRateDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new UpdateCurrencyRateCommand(id, dto.ExchangeRate), ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Currency not found" });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Currency.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _mediator.Send(new DeleteCurrencyCommand(id), ct);
        return deleted ? NoContent() : NotFound(new { error = "Currency not found" });
    }

    [HttpGet("rates")]
    [RequirePermission(Permissions.Currency.Read)]
    public async Task<IActionResult> GetRates(CancellationToken ct)
    {
        var rates = await _mediator.Send(new GetCurrencyRatesQuery(), ct);
        return Ok(new { rates });
    }

    [HttpPost("exchange")]
    [RequirePermission(Permissions.Currency.Manage)]
    public async Task<IActionResult> ExchangeCurrency([FromBody] ExchangeCurrencyDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new ExchangeCurrencyCommand(
                dto.FromCurrency, dto.ToCurrency, dto.Amount, dto.Source), ct);
            return Ok(new { exchangedAmount = result.ExchangedAmount, rate = result.Rate });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record CreateCurrencyDto(string Code, string Name, decimal ExchangeRate);
public record UpdateCurrencyRateDto(decimal ExchangeRate);
public record ExchangeCurrencyDto(string FromCurrency, string ToCurrency, decimal Amount, string Source = "Manual");
