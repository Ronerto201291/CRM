using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;

namespace Erp.Modules.Inventory.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/inventory/serials")]
    [ApiVersion("1.0")]
    public class SerialsController : ControllerBase
    {
        private readonly IInventoryDbContext _context;

        public SerialsController(IInventoryDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var serials = await _context.SerialNumbers.AsNoTracking().ToListAsync(ct);
            return Ok(serials);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var serial = await _context.SerialNumbers.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (serial == null) return NotFound();
            return Ok(serial);
        }

        public class CreateSerialDto
        {
            public Guid ProductId { get; set; }
            public Guid? LotId { get; set; }
            public string Serial { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSerialDto dto, CancellationToken ct)
        {
            var serial = new SerialNumber
            {
                ProductId = dto.ProductId,
                LotId = dto.LotId,
                Serial = dto.Serial,
                Status = "Available"
            };

            _context.SerialNumbers.Add(serial);
            await _context.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(Get), new { id = serial.Id }, serial);
        }

        public class UpdateSerialStatusDto
        {
            public string Status { get; set; } = string.Empty;
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSerialStatusDto dto, CancellationToken ct)
        {
            var serial = await _context.SerialNumbers.FindAsync(new object[] { id }, ct);
            if (serial == null) return NotFound();

            serial.Status = dto.Status;
            if (dto.Status == "Sold") serial.SoldDate = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            return Ok(serial);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var serial = await _context.SerialNumbers.FindAsync(new object[] { id }, ct);
            if (serial == null) return NotFound();

            _context.SerialNumbers.Remove(serial);
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
