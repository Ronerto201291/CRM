using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Commands;

public class CreateContactCommand : IRequest<ContactDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Position { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? SupplierId { get; set; }
}

public class UpdateContactCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Position { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? SupplierId { get; set; }
}

public class DeleteContactCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}

public class AnonymizeContactCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}
