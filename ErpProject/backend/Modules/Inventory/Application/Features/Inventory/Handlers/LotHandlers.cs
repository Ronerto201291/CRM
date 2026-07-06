using Erp.Application.Common.Interfaces;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Handlers;

public class GetLotsHandler : IRequestHandler<GetLotsQuery, List<Lot>>
{
    private readonly IInventoryDbContext _ctx;
    public GetLotsHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<List<Lot>> Handle(GetLotsQuery request, CancellationToken ct)
        => await _ctx.Lots.AsNoTracking().ToListAsync(ct);
}

public class GetLotByIdHandler : IRequestHandler<GetLotByIdQuery, Lot?>
{
    private readonly IInventoryDbContext _ctx;
    public GetLotByIdHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<Lot?> Handle(GetLotByIdQuery request, CancellationToken ct)
        => await _ctx.Lots.FirstOrDefaultAsync(l => l.Id == request.Id, ct);
}

public class CreateLotHandler : IRequestHandler<CreateLotCommand, Lot>
{
    private readonly IInventoryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateLotHandler(IInventoryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<Lot> Handle(CreateLotCommand request, CancellationToken ct)
    {
        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context."),
            ProductId = request.ProductId,
            LotNumber = request.LotNumber,
            ExpirationDate = request.ExpirationDate,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            IsActive = true,
        };

        _ctx.Lots.Add(lot);
        await _ctx.SaveChangesAsync(ct);
        return lot;
    }
}

public class UpdateLotHandler : IRequestHandler<UpdateLotCommand, Lot?>
{
    private readonly IInventoryDbContext _ctx;
    public UpdateLotHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<Lot?> Handle(UpdateLotCommand request, CancellationToken ct)
    {
        var lot = await _ctx.Lots.FindAsync([request.Id], ct);
        if (lot == null) return null;

        lot.LotNumber = request.LotNumber;
        lot.ExpirationDate = request.ExpirationDate;
        lot.Quantity = request.Quantity;
        lot.UnitCost = request.UnitCost;

        await _ctx.SaveChangesAsync(ct);
        return lot;
    }
}

public class DeleteLotHandler : IRequestHandler<DeleteLotCommand, bool>
{
    private readonly IInventoryDbContext _ctx;
    public DeleteLotHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(DeleteLotCommand request, CancellationToken ct)
    {
        var lot = await _ctx.Lots.FindAsync([request.Id], ct);
        if (lot == null) return false;

        _ctx.Lots.Remove(lot);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
