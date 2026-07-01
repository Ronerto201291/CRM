using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Commands;

public class UpdateClientCommand : IRequest<ClientDto>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? CustomFields { get; set; }
}
