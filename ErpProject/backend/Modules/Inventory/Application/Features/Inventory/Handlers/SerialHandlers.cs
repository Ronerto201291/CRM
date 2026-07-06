using Erp.Application.Common.Interfaces;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Handlers;

public class GetSerialsHandler : IRequestHandler<GetSerialsQuery, List<SerialNumber>>
{
    private readonly IInventoryDbContext _ctx;
    public GetSerialsHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<List<SerialNumber>> Handle(GetSerialsQuery request, CancellationToken ct)
        => await _ctx.SerialNumbers.AsNoTracking().ToListAsync(ct);
}

public class GetSerialByIdHandler : IRequestHandler<GetSerialByIdQuery, SerialNumber?>
{
    private readonly IInventoryDbContext _ctx;
    public GetSerialByIdHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<SerialNumber?> Handle(GetSerialByIdQuery request, CancellationToken ct)
        => await _ctx.SerialNumbers.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
}

public class CreateSerialHandler : IRequestHandler<CreateSerialCommand, SerialNumber>
{
    private readonly IInventoryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateSerialHandler(IInventoryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<SerialNumber> Handle(CreateSerialCommand request, CancellationToken ct)
    {
        var serial = new SerialNumber
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context."),
            ProductId = request.ProductId,
            LotId = request.LotId,
            Serial = request.Serial,
            Status = "Available",
        };

        _ctx.SerialNumbers.Add(serial);
        await _ctx.SaveChangesAsync(ct);
        return serial;
    }
}

public class UpdateSerialStatusHandler : IRequestHandler<UpdateSerialStatusCommand, SerialNumber?>
{
    private readonly IInventoryDbContext _ctx;
    public UpdateSerialStatusHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<SerialNumber?> Handle(UpdateSerialStatusCommand request, CancellationToken ct)
    {
        var serial = await _ctx.SerialNumbers.FindAsync([request.Id], ct);
        if (serial == null) return null;

        serial.Status = request.Status;
        if (request.Status == "Sold") serial.SoldDate = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return serial;
    }
}

public class DeleteSerialHandler : IRequestHandler<DeleteSerialCommand, bool>
{
    private readonly IInventoryDbContext _ctx;
    public DeleteSerialHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(DeleteSerialCommand request, CancellationToken ct)
    {
        var serial = await _ctx.SerialNumbers.FindAsync([request.Id], ct);
        if (serial == null) return false;

        _ctx.SerialNumbers.Remove(serial);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
