using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Commands;

public class CreateLeadCommand : IRequest<LeadDto>
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? Status { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

public class UpdateLeadCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? Status { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

public class ConvertLeadToClientCommand : IRequest<ConvertLeadToClientResult>
{
    public Guid LeadId { get; set; }
}

public class ConvertLeadToClientResult
{
    public string Message { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public object? Client { get; set; }
}

public class DeleteLeadCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}
