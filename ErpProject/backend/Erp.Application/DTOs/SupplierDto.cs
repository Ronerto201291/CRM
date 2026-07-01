namespace Erp.Application.DTOs;

public class SupplierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupplierDetailDto : SupplierDto
{
    public string? BankAccount { get; set; }
    public List<SupplierActivityDto> Activities { get; set; } = new();
    public List<SupplierExpenseDto> Expenses { get; set; } = new();
}

public class SupplierActivityDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SupplierExpenseDto
{
    public Guid Id { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal? Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? IssueDate { get; set; }
}
