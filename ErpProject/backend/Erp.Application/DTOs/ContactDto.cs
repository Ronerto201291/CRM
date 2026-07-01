namespace Erp.Application.DTOs;

public class ContactDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? ClientName { get; set; }
    public string? SupplierName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
