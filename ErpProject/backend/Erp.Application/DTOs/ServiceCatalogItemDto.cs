namespace Erp.Application.DTOs;

public class ServiceCatalogItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultPrice { get; set; }
    public decimal DefaultTaxRate { get; set; }
    public string DefaultPeriodicity { get; set; } = "Monthly";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
