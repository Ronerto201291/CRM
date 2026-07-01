using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Queries;

public class GetSuppliersQuery : IRequest<List<SupplierDto>>
{
    public string? Search { get; set; }
}

public class GetSupplierByIdQuery : IRequest<SupplierDetailDto?>
{
    public Guid Id { get; set; }
}
