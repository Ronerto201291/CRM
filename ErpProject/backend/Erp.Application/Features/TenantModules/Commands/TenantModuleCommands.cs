using MediatR;

namespace Erp.Application.Features.TenantModules.Commands;

public class UpdateTenantModuleCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public bool IsEnabled { get; set; }
}
