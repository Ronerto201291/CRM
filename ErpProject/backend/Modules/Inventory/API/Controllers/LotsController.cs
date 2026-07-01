using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;

namespace Erp.Modules.Inventory.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/inventory/lots")]
    [ApiVersion("1.0")]
    public class LotsController : ControllerBase
    {
        private readonly IInventoryDbContext _context;

        public LotsController(IInventoryDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var lots = await _context.Lots.AsNoTracking().ToListAsync(ct);
            return Ok(lots);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var lot = await _context.Lots.FirstOrDefaultAsync(l => l.Id == id, ct);
            if (lot == null) return NotFound();
            return Ok(lot);
        }

        public class CreateLotDto
        {
            public Guid ProductId { get; set; }
            public string LotNumber { get; set; } = string.Empty;
            public DateTime ExpirationDate { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitCost { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLotDto dto, CancellationToken ct)
        {
            var lot = new Lot
            {
                ProductId = dto.ProductId,
                LotNumber = dto.LotNumber,
                ExpirationDate = dto.ExpirationDate,
                Quantity = dto.Quantity,
                UnitCost = dto.UnitCost,
                IsActive = true
            };

            _context.Lots.Add(lot);
            await _context.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(Get), new { id = lot.Id }, lot);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateLotDto dto, CancellationToken ct)
        {
            var lot = await _context.Lots.FindAsync(new object[] { id }, ct);
            if (lot == null) return NotFound();

            lot.LotNumber = dto.LotNumber;
            lot.ExpirationDate = dto.ExpirationDate;
            lot.Quantity = dto.Quantity;
            lot.UnitCost = dto.UnitCost;

            await _context.SaveChangesAsync(ct);
            return Ok(lot);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var lot = await _context.Lots.FindAsync(new object[] { id }, ct);
            if (lot == null) return NotFound();

            _context.Lots.Remove(lot);
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
