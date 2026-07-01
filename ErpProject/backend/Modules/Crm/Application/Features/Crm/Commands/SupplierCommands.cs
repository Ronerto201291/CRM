using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Commands;

public class CreateSupplierCommand : IRequest<SupplierDto>
{
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? BankAccount { get; set; }
}

public class UpdateSupplierCommand : IRequest<SupplierDto?>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? BankAccount { get; set; }
}

public class AnonymizeSupplierCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}
