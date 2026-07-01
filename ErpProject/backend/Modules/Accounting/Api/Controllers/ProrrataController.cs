using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/prorrata")]
public class ProrrataController : ControllerBase
{
    [HttpPost("calculate")]
    public IActionResult CalculateProrrata([FromBody] CalculateProrrataRequest request)
    {
        var totalOperations = request.DeductibleOperations + request.NonDeductibleOperations;
        var prorataProportion = totalOperations > 0 ? request.DeductibleOperations / totalOperations : 0;
        var deductibleVat = request.TotalVatSupported * prorataProportion;

        return Created("", new
        {
            id = Guid.NewGuid(),
            deductibleOperations = request.DeductibleOperations,
            nonDeductibleOperations = request.NonDeductibleOperations,
            prorataProportion = Math.Round(prorataProportion * 100, 2),
            totalVatSupported = request.TotalVatSupported,
            deductibleVat = deductibleVat,
            nonDeductibleVat = request.TotalVatSupported - deductibleVat,
            message = $"Prorrata calculada: {Math.Round(prorataProportion * 100, 2)}%"
        });
    }

    [HttpGet("types")]
    public IActionResult GetProrrataTypes()
    {
        return Ok(new[]
        {
            new { id = "General", name = "Prorrata General", description = "Para empresas con operaciones mixtas" },
            new { id = "Special", name = "Prorrata Especial", description = "Sectores específicos" }
        });
    }
}

public class CalculateProrrataRequest
{
    public decimal DeductibleOperations { get; set; }
    public decimal NonDeductibleOperations { get; set; }
    public decimal TotalVatSupported { get; set; }
}

