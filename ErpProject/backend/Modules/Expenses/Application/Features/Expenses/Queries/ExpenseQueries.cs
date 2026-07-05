using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Expenses.Application.Features.Expenses.Queries;

public class ExpenseUploadDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime UploadedAt { get; set; }
    public Guid? ExpenseDocumentId { get; set; }
}

public class ExpenseDocumentListDto
{
    public Guid Id { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierTaxId { get; set; }
    public DateTime? IssueDate { get; set; }
    public decimal? TaxBase { get; set; }
    public decimal? VATRate { get; set; }
    public decimal? VATAmount { get; set; }
    public decimal? IRPFRate { get; set; }
    public decimal? IRPFAmount { get; set; }
    public decimal? Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsValidated { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal? OcrConfidence { get; set; }
    public int LineCount { get; set; }
}

public class ExpenseLineDetailDto
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? VATRate { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? ProductId { get; set; }
}

public class ExpenseDocumentDetailDto
{
    public Guid Id { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierTaxId { get; set; }
    public Guid? SupplierId { get; set; }
    public DateTime? IssueDate { get; set; }
    public decimal? TaxBase { get; set; }
    public decimal? VATRate { get; set; }
    public decimal? VATAmount { get; set; }
    public decimal? IRPFRate { get; set; }
    public decimal? IRPFAmount { get; set; }
    public decimal? Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsValidated { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public bool IsLocked { get; set; }
    public string? HashSignature { get; set; }
    public decimal? OcrConfidence { get; set; }
    public string? OcrRawData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ExpenseLineDetailDto> Lines { get; set; } = new();
}

public class ExpenseStatsDto
{
    public int Pending { get; set; }
    public int Approved { get; set; }
    public decimal TotalVATSoportado { get; set; }
    public decimal TotalBase { get; set; }
}

public class GetExpenseUploadsQuery : IRequest<List<ExpenseUploadDto>> { }

public class GetExpenseDocumentsQuery : IRequest<List<ExpenseDocumentListDto>> { }

public class GetExpenseByIdQuery : IRequest<ExpenseDocumentDetailDto?>
{
    public Guid Id { get; set; }
}

public class GetExpenseStatsQuery : IRequest<ExpenseStatsDto> { }

public class GetSupplierExpensesQuery : IRequest<List<SupplierExpenseDto>>
{
    public Guid SupplierId { get; set; }
}

public record ExpenseAnomalyItemDto(
    Guid ExpenseId,
    string? InvoiceNumber,
    string? SupplierName,
    decimal Amount,
    DateTime? IssueDate,
    string Type,
    string Message);

public record ExpenseAnomaliesDto(
    IReadOnlyList<ExpenseAnomalyItemDto> Outliers,
    IReadOnlyList<ExpenseAnomalyItemDto> Duplicates,
    string? AiSummary = null);

public record ExpenseCategorySuggestionDto(
    string AccountCode,
    string AccountName,
    string Reason,
    decimal Confidence);

public class GetExpenseAnomaliesQuery : IRequest<ExpenseAnomaliesDto> { }

public class SuggestExpenseCategoryQuery : IRequest<ExpenseCategorySuggestionDto?>
{
    public Guid? SupplierId { get; set; }
    public string? SupplierTaxId { get; set; }
    public string? Description { get; set; }
}
