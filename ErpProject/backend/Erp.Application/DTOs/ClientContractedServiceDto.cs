namespace Erp.Application.DTOs;

public class ClientContractedServiceDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid ServiceCatalogItemId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal TaxRate { get; set; }
    public string Periodicity { get; set; } = "Monthly";
    public DateTime StartDate { get; set; }
    public DateTime NextBillingDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Active";
    public Guid? LastInvoiceId { get; set; }
    public DateTime CreatedAt { get; set; }
}
