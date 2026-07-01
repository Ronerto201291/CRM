namespace Erp.Application.DTOs;
public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Stock { get; set; }
}
