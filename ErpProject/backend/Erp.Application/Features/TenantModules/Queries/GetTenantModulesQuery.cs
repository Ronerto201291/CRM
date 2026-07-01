using MediatR;

namespace Erp.Application.Features.TenantModules.Queries;

public class TenantModuleDto
{
    public Guid Id { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GetTenantModulesQuery : IRequest<List<TenantModuleDto>>
{
}
