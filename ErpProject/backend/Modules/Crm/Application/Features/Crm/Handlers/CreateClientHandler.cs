using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Handlers;

public class CreateClientHandler : IRequestHandler<CreateClientCommand, ClientDto>
{
    private readonly ICrmDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IPublisher _publisher;

    public CreateClientHandler(ICrmDbContext context, ITenantContext tenantContext, IPublisher publisher)
    {
        _context = context;
        _tenantContext = tenantContext;
        _publisher = publisher;
    }

    public async Task<ClientDto> Handle(CreateClientCommand request, CancellationToken ct)
    {
        var companyId = _tenantContext.TenantId ?? Guid.Empty;

        var client = new Client
        {
            Id           = Guid.NewGuid(),
            CompanyId    = companyId,
            Name         = request.Name,
            TaxId        = request.TaxId,
            Email        = request.Email,
            Phone        = request.Phone,
            Address      = request.Address,
            CustomFields = request.CustomFields,
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync(ct);

        await _publisher.Publish(new ClientCreatedEvent
        {
            ClientId  = client.Id,
            CompanyId = companyId,
            Name      = client.Name,
            Email     = client.Email,
            TaxId     = client.TaxId,
        }, ct);

        return new ClientDto { Id = client.Id, Name = client.Name, TaxId = client.TaxId, Email = client.Email, Phone = client.Phone, Address = client.Address, CustomFields = client.CustomFields, CreatedAt = client.CreatedAt };
    }
}
