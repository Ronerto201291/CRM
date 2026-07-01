using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Handlers;

// ── Queries ──
public record GetClientsModuleQuery(string? Search) : IRequest<List<ClientSummaryDto>>;

public record ClientSummaryDto(Guid Id, string Name, string TaxId, string Email, string Phone, DateTime CreatedAt);

public class GetClientsModuleHandler : IRequestHandler<GetClientsModuleQuery, List<ClientSummaryDto>>
{
    private readonly ICrmDbContext _ctx;
    public GetClientsModuleHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<List<ClientSummaryDto>> Handle(GetClientsModuleQuery request, CancellationToken ct)
    {
        var q = _ctx.Clients.AsQueryable();
        if (!string.IsNullOrEmpty(request.Search))
            q = q.Where(c => c.Name.Contains(request.Search) || c.TaxId.Contains(request.Search));

        return await q.OrderBy(c => c.Name)
            .Select(c => new ClientSummaryDto(c.Id, c.Name, c.TaxId, c.Email, c.Phone, c.CreatedAt))
            .ToListAsync(ct);
    }
}

// ── Commands ──
public record CreateClientModuleCommand(string Name, string TaxId, string Email, string Phone, string Address)
    : IRequest<Guid>;

public class CreateClientModuleHandler : IRequestHandler<CreateClientModuleCommand, Guid>
{
    private readonly ICrmDbContext _ctx;
    public CreateClientModuleHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<Guid> Handle(CreateClientModuleCommand request, CancellationToken ct)
    {
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            TaxId = request.TaxId,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address
        };
        _ctx.Clients.Add(client);
        await _ctx.SaveChangesAsync(ct);
        return client.Id;
    }
}

// ── Supplier Handlers ──
public record GetSuppliersModuleQuery(string? Search) : IRequest<List<SupplierSummaryDto>>;
public record SupplierSummaryDto(Guid Id, string Name, string TaxId, string Email, bool IsActive);

public class GetSuppliersModuleHandler : IRequestHandler<GetSuppliersModuleQuery, List<SupplierSummaryDto>>
{
    private readonly ICrmDbContext _ctx;
    public GetSuppliersModuleHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<List<SupplierSummaryDto>> Handle(GetSuppliersModuleQuery request, CancellationToken ct)
    {
        var q = _ctx.Suppliers.AsQueryable();
        if (!string.IsNullOrEmpty(request.Search))
            q = q.Where(s => s.Name.Contains(request.Search) || s.TaxId.Contains(request.Search));

        return await q.OrderBy(s => s.Name)
            .Select(s => new SupplierSummaryDto(s.Id, s.Name, s.TaxId, s.Email, s.IsActive))
            .ToListAsync(ct);
    }
}
