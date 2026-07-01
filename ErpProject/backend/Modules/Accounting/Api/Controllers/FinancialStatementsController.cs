using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/financial-statements")]
public class FinancialStatementsController : ControllerBase
{
    [HttpPost("cash-flow")]
    public IActionResult GenerateCashFlow([FromBody] object request)
    {
        return Ok(new
        {
            id = Guid.NewGuid(),
            operatingCashFlow = 150000m,
            investingCashFlow = -50000m,
            financingCashFlow = 20000m,
            netCashFlow = 120000m,
            period = "01/2025",
            status = "Generated"
        });
    }

    [HttpPost("equity")]
    public IActionResult GenerateEquityStatement([FromBody] object request)
    {
        return Ok(new
        {
            id = Guid.NewGuid(),
            beginningCapital = 100000m,
            netIncome = 45000m,
            dividendsPaid = 10000m,
            otherChanges = 5000m,
            endingCapital = 140000m,
            status = "Generated"
        });
    }
}
