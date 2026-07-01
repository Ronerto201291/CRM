using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;

namespace Erp.Modules.Purchasing.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/purchasing/orders")]
[ApiVersion("1.0")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchasingDbContext _context;

    public PurchaseOrdersController(IPurchasingDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _context.PurchaseOrders
            .Include(p => p.Lines)
            .AsNoTracking()
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (po == null) return NotFound();
        return Ok(po);
    }

    public class CreatePoDto
    {
        public string Number { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public List<CreatePoLineDto> Lines { get; set; } = new();
    }

    public class CreatePoLineDto
    {
        public Guid? ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePoDto dto, CancellationToken ct)
    {
        var po = new PurchaseOrder
        {
            Number = dto.Number,
            OrderDate = dto.OrderDate
        };

        foreach (var l in dto.Lines)
        {
            var line = new PurchaseOrderLine
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice
            };
            po.Lines.Add(line);
            _context.PurchaseOrderLines.Add(line);
        }

        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = po.Id }, po);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreatePoDto dto, CancellationToken ct)
    {
        var po = await _context.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (po == null) return NotFound();

        po.Number = dto.Number;
        po.OrderDate = dto.OrderDate;

        // Simple replace lines: delete existing and add new
        var existing = po.Lines.ToList();
        foreach (var e in existing)
            _context.PurchaseOrderLines.Remove(e);

        po.Lines.Clear();
        foreach (var l in dto.Lines)
        {
            var line = new PurchaseOrderLine
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice
            };
            po.Lines.Add(line);
            _context.PurchaseOrderLines.Add(line);
        }

        await _context.SaveChangesAsync(ct);
        return Ok(po);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var po = await _context.PurchaseOrders.FindAsync(new object[] { id }, ct);
        if (po == null) return NotFound();
        _context.PurchaseOrders.Remove(po);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
