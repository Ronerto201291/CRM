using MediatR;

namespace Erp.Modules.Expenses.Application.Features.Expenses.Commands;

public class CreateExpenseDocumentCommand : IRequest<Guid>
{
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
}

public class ExpenseLineDraftDto
{
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal? VATRate { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? ProductId { get; set; }
}

public class UpdateExpenseDraftCommand : IRequest<bool>
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
    public List<ExpenseLineDraftDto>? Lines { get; set; }
}

public class ApproveExpenseCommand : IRequest<ApproveExpenseResult>
{
    public Guid Id { get; set; }
}

public class SubmitExpenseForApprovalCommand : IRequest<SubmitExpenseForApprovalResult>
{
    public Guid Id { get; set; }
}

public class SubmitExpenseForApprovalResult
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool AutoApproved { get; set; }
    public ApproveExpenseResult? Approval { get; set; }
}

public class ApproveExpenseResult
{
    public string Message { get; set; } = string.Empty;
    public string HashSignature { get; set; } = string.Empty;
    public int LinesWithInventory { get; set; }
}

public class AddExpenseLineCommand : IRequest<Guid>
{
    public Guid ExpenseDocumentId { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal? VATRate { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? ProductId { get; set; }
}

public class UpdateExpenseLineCommand : IRequest<bool>
{
    public Guid ExpenseDocumentId { get; set; }
    public Guid LineId { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? VATRate { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? ProductId { get; set; }
}

public class DeleteExpenseLineCommand : IRequest<bool>
{
    public Guid ExpenseDocumentId { get; set; }
    public Guid LineId { get; set; }
}
