using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Crm.Handlers;

public class GetClientsHandler : IRequestHandler<GetClientsQuery, PaginatedClientsResult>
{
    private readonly ICrmDbContext _context;
    public GetClientsHandler(ICrmDbContext context) => _context = context;

    public async Task<PaginatedClientsResult> Handle(GetClientsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);

        var query = _context.Clients.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.Email.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                Name = c.Name,
                TaxId = c.TaxId,
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                CustomFields = c.CustomFields,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync(ct);

        return new PaginatedClientsResult(items, totalCount, page, pageSize);
    }
}

public class GetClientByIdHandler : IRequestHandler<GetClientByIdQuery, ClientDto?>
{
    private readonly ICrmDbContext _context;

    public GetClientByIdHandler(ICrmDbContext context) => _context = context;

    public async Task<ClientDto?> Handle(GetClientByIdQuery request, CancellationToken ct)
        => await _context.Clients
            .Where(c => c.Id == request.Id)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                Name = c.Name,
                TaxId = c.TaxId,
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                CustomFields = c.CustomFields,
                CreatedAt = c.CreatedAt,
            })
            .FirstOrDefaultAsync(ct);
}
