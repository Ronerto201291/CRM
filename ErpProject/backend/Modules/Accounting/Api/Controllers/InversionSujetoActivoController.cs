using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/isp")]
public class InversionSujetoActivoController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        var isp = new[]
        {
            new 
            { 
                id = Guid.NewGuid(),
                description = "Servicios de consultoría",
                supplierVat = "IT12345678901",
                invoiceAmount = 50000m,
                vatRate = 0.22m,
                vatAmount = 11000m,
                ispApplies = true,
                status = "Active"
            },
            new 
            { 
                id = Guid.NewGuid(),
                description = "Transportes intracomunitarios",
                supplierVat = "DE98765432101",
                invoiceAmount = 75000m,
                vatRate = 0.19m,
                vatAmount = 14250m,
                ispApplies = false,
                status = "Active"
            },
        };
        return Ok(isp);
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateISPRequest request)
    {
        return Created("", new
        {
            id = Guid.NewGuid(),
            description = request.Description,
            supplierVat = request.SupplierVat,
            invoiceAmount = request.InvoiceAmount,
            ispApplies = DeterminateISP(request.SupplierVat, request.Description),
            status = "Created",
            message = "Inversión del sujeto pasivo registrada"
        });
    }

    [HttpGet("{id}")]
    public IActionResult GetById(Guid id)
    {
        return Ok(new
        {
            id,
            description = "Servicios profesionales",
            supplierVat = "IT12345678901",
            invoiceAmount = 50000m,
            vatAmount = 11000m,
            ispApplies = true,
            status = "Active",
            regulation = "Art. 84.1 RD 1619/2012"
        });
    }

    [HttpPost("{id}/calculate-vat")]
    public IActionResult CalculateVAT(Guid id, [FromBody] CalculateVATRequest request)
    {
        return Ok(new
        {
            id,
            invoiceVat = request.Amount * 0.22m,
            ispApplied = true,
            effectiveVat = 0,
            message = "IVA con ISP aplicado",
            status = "Calculated"
        });
    }

    private static bool DeterminateISP(string supplierVat, string description)
    {
        // Simplificado: solo EU suppliers, ciertos servicios
        return supplierVat.Length > 2 && 
               (description.Contains("consultor") || 
                description.Contains("profesional") ||
                description.Contains("servicio"));
    }
}

public class CreateISPRequest
{
    public string Description { get; set; } = string.Empty;
    public string SupplierVat { get; set; } = string.Empty;
    public decimal InvoiceAmount { get; set; }
}

public class CalculateVATRequest
{
    public decimal Amount { get; set; }
}
