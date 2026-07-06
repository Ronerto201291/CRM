using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Billing.Application.Features.Billing.Queries;

public record PaginatedInvoicesResult(
    IReadOnlyList<InvoiceDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetInvoicesQuery : IRequest<PaginatedInvoicesResult>
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GetInvoiceByIdQuery : IRequest<InvoiceDto?>
{
    public Guid Id { get; set; }
}

/// <summary>Versión reducida para el portal público del cliente (sin datos internos, ADR-0018 #39).</summary>
public record InvoicePublicDto(
    string Number,
    string Series,
    int FiscalYear,
    string Status,
    bool IsLocked,
    string CompanyName,
    string? CompanyAddress,
    DateTime IssueDate,
    DateTime DueDate,
    decimal Subtotal,
    decimal TaxAmount,
    decimal IrpfAmount,
    decimal SurchargeAmount,
    decimal Total,
    List<PublicInvoiceLineDto> Lines);

/// <summary>Lookup por token para el portal del cliente (sin auth, ADR-0018 #39).</summary>
public class GetInvoiceByTokenQuery : IRequest<InvoicePublicDto?>
{
    public string Token { get; set; } = string.Empty;
}
