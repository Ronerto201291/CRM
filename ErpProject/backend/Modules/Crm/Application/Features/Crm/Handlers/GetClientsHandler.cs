using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Crm.Handlers;

public class GetClientsHandler : IRequestHandler<GetClientsQuery, List<ClientDto>>
{
    private readonly ICrmDbContext _context;
    public GetClientsHandler(ICrmDbContext context) => _context = context;

    public async Task<List<ClientDto>> Handle(GetClientsQuery request, CancellationToken ct)
    {
        var query = _context.Clients.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            query = query.Where(c => c.Name.Contains(request.SearchTerm) || c.Email.Contains(request.SearchTerm));

        return await query.Select(c => new ClientDto
        {
            Id = c.Id, Name = c.Name, TaxId = c.TaxId, Email = c.Email,
            Phone = c.Phone, Address = c.Address, CustomFields = c.CustomFields, CreatedAt = c.CreatedAt
        }).ToListAsync(ct);
    }
}
