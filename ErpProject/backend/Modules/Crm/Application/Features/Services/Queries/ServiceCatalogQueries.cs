using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Services.Queries;

public class GetServiceCatalogQuery : IRequest<List<ServiceCatalogItemDto>>
{
    public bool IncludeInactive { get; set; }
}
