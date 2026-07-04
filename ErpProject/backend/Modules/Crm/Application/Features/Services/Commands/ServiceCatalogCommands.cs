using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Services.Commands;

public class CreateServiceCatalogItemCommand : IRequest<ServiceCatalogItemDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultPrice { get; set; }
    public decimal DefaultTaxRate { get; set; }
    /// <summary>"Monthly" | "Quarterly" | "Yearly"</summary>
    public string DefaultPeriodicity { get; set; } = "Monthly";
}

public class UpdateServiceCatalogItemCommand : IRequest<ServiceCatalogItemDto>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultPrice { get; set; }
    public decimal DefaultTaxRate { get; set; }
    public string DefaultPeriodicity { get; set; } = "Monthly";
    public bool IsActive { get; set; } = true;
}
