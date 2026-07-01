using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Crm.Handlers;

public class UpdateClientHandler : IRequestHandler<UpdateClientCommand, ClientDto>
{
    private readonly ICrmDbContext _context;
    private readonly IPublisher _publisher;

    public UpdateClientHandler(ICrmDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<ClientDto> Handle(UpdateClientCommand request, CancellationToken ct)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Client {request.Id} not found.");
        client.Name         = request.Name;
        client.TaxId        = request.TaxId;
        client.Email        = request.Email;
        client.Phone        = request.Phone;
        client.Address      = request.Address;
        client.CustomFields = request.CustomFields;
        await _context.SaveChangesAsync(ct);

        await _publisher.Publish(new ClientUpdatedEvent
        {
            ClientId  = client.Id,
            CompanyId = client.CompanyId,
            Name      = client.Name,
        }, ct);

        return new ClientDto { Id = client.Id, Name = client.Name, TaxId = client.TaxId, Email = client.Email, Phone = client.Phone, Address = client.Address, CustomFields = client.CustomFields, CreatedAt = client.CreatedAt };
    }
}
