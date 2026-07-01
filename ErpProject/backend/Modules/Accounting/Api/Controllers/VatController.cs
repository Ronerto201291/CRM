using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/vat")]
public class VatController : ControllerBase
{
    [HttpPost("calculate")]
    public IActionResult CalculateVat([FromBody] CalculateVatRequest request)
    {
        var vatAmount = request.Amount * request.VatRate;
        var total = request.Amount + vatAmount;

        return Ok(new
        {
            id = Guid.NewGuid(),
            amount = request.Amount,
            vatRate = request.VatRate,
            vatAmount = vatAmount,
            total = total,
            status = "Calculated"
        });
    }

    [HttpGet("rates")]
    public IActionResult GetVatRates()
    {
        return Ok(new[]
        {
            new { type = "Standard", rate = 0.21m, applies = "General supplies" },
            new { type = "Reduced", rate = 0.10m, applies = "Food, books" },
            new { type = "SuperReduced", rate = 0.04m, applies = "Essential goods" },
            new { type = "Zero", rate = 0m, applies = "Exports" }
        });
    }

    [HttpPost("declare/modelo330")]
    public IActionResult DeclareModelo330([FromBody] object dto)
    {
        return Created("", new
        {
            id = Guid.NewGuid(),
            modelo = "330",
            status = "Declared",
            message = "Modelo 330 declarado exitosamente"
        });
    }
}

public class CalculateVatRequest
{
    public decimal Amount { get; set; }
    public decimal VatRate { get; set; } = 0.21m;
}

