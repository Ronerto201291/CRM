using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Services.Commands;

public class CreateClientContractedServiceCommand : IRequest<ClientContractedServiceDto>
{
    public Guid ClientId { get; set; }
    public Guid ServiceCatalogItemId { get; set; }
    /// <summary>Override opcional del precio del catálogo (negociación particular con el cliente).</summary>
    public decimal? PriceOverride { get; set; }
    public decimal? TaxRateOverride { get; set; }
    /// <summary>"Monthly" | "Quarterly" | "Yearly"; si es null, se usa la periodicidad por defecto del catálogo.</summary>
    public string? PeriodicityOverride { get; set; }
    public DateTime StartDate { get; set; }
}

public class CancelClientContractedServiceCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public DateTime? EndDate { get; set; }
}
