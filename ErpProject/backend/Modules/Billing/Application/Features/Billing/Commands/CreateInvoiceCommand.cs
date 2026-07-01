using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Billing.Application.Features.Billing.Commands;

public class CreateInvoiceCommand : IRequest<InvoiceDto>
{
    /// <summary>Null para clientes manuales/B2C (ClientType = "Manual").</summary>
    public Guid? ClientId { get; set; }
    /// <summary>"Registered" = cliente del CRM | "Manual" = datos introducidos manualmente.</summary>
    public string ClientType { get; set; } = "Registered";
    // Campos para clientes manuales (se ignoran si ClientType = "Registered")
    public string? ClientName    { get; set; }
    public string? ClientTaxId   { get; set; }
    public string? ClientEmail   { get; set; }
    public string? ClientAddress { get; set; }

    public string Series { get; set; } = "A";
    public DateTime DueDate { get; set; }
    /// <summary>Fecha de operación si difiere de la de expedición (RD 1619/2012 Art. 6.1.e).</summary>
    public DateTime? OperationDate { get; set; }
    public string? InvoiceType { get; set; } = "Normal"; // Normal, Rectificativa, Simplificada
    public Guid? RectifiedInvoiceId { get; set; }
    /// <summary>Código A–I (Art. 15.1 RD 1619/2012) obligatorio si Rectificativa.</summary>
    public string? RectificationReasonCode { get; set; }
    public string? RectificationReasonText { get; set; }
    public DateTime? RectificationPeriodFrom { get; set; }
    public DateTime? RectificationPeriodTo { get; set; }
    /// <summary>Si true y líneas IntraComunitario, valida NIF-IVA en VIES y guarda resultado.</summary>
    public bool ValidateEuVatWithVies { get; set; }

    public decimal IrpfRate { get; set; } // 0 or 15 for professionals
    public List<CreateInvoiceLineDto> Lines { get; set; } = new();
}

public class CreateInvoiceLineDto
{
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }        // 21, 10, 4, 0
    public decimal SurchargeRate { get; set; }  // 5.2, 1.4, 0.5, 0
    /// <summary>"Nacional" | "IntraComunitario" | "Exportacion"</summary>
    public string TipoOperacion { get; set; } = "Nacional";
}
