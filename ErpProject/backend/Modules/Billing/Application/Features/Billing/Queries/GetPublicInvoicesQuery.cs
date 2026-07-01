using MediatR;

namespace Erp.Modules.Billing.Application.Features.Billing.Queries;

public class PublicInvoiceListDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string? Hash { get; set; }
}

public class PublicInvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
}

public class PublicInvoiceDetailDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public string InvoiceType { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal IrpfAmount { get; set; }
    public decimal Total { get; set; }
    public string? Hash { get; set; }
    public string? PreviousHash { get; set; }
    public List<PublicInvoiceLineDto> Lines { get; set; } = new();
}

public class GetPublicInvoicesQuery : IRequest<List<PublicInvoiceListDto>>
{
    public int? Year { get; set; }
    public string? Status { get; set; }
}

public class GetPublicInvoiceByIdQuery : IRequest<PublicInvoiceDetailDto?>
{
    public Guid Id { get; set; }
}
