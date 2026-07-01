using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/aging")]
public class AgingController : ControllerBase
{
    [HttpGet("receivables")]
    public IActionResult GetReceivablesAging()
    {
        var aging = new
        {
            totalReceipts = 125000m,
            current = new { days = "0-30", amount = 45000m },
            past30 = new { days = "31-60", amount = 35000m },
            past60 = new { days = "61-90", amount = 25000m },
            past90 = new { days = "90+", amount = 20000m },
            dso = 45.5m, // Days Sales Outstanding
        };
        return Ok(aging);
    }

    [HttpGet("payables")]
    public IActionResult GetPayablesAging()
    {
        var aging = new
        {
            totalPayments = 85000m,
            current = new { days = "0-30", amount = 30000m },
            past30 = new { days = "31-60", amount = 25000m },
            past60 = new { days = "61-90", amount = 20000m },
            past90 = new { days = "90+", amount = 10000m },
            dpo = 38.2m, // Days Payable Outstanding
        };
        return Ok(aging);
    }

    [HttpPost("calculate")]
    public IActionResult CalculateAging([FromBody] object dto)
    {
        return Created("", new { id = Guid.NewGuid(), status = "Calculated", message = "Informe de antigüedad calculado" });
    }
}
