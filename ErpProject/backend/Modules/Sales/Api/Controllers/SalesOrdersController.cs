using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;

namespace Erp.Modules.Sales.Api.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/sales/orders")]
    [ApiVersion("1.0")]
    public class SalesOrdersController : ControllerBase
    {
        private readonly ISalesDbContext _context;

        public SalesOrdersController(ISalesDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var list = await _context.SalesOrders.Include(s => s.Lines).AsNoTracking().ToListAsync(ct);
            return Ok(list);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var so = await _context.SalesOrders.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id, ct);
            if (so == null) return NotFound();
            return Ok(so);
        }

        public class CreateSoDto
        {
            public string Number { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
            public Guid? ClientId { get; set; }
            public string ClientName { get; set; } = string.Empty;
            public List<CreateSoLineDto> Lines { get; set; } = new();
        }

        public class CreateSoLineDto
        {
            public Guid? ProductId { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSoDto dto, CancellationToken ct)
        {
            var so = new SalesOrder
            {
                Number = dto.Number,
                OrderDate = dto.OrderDate,
                ClientId = dto.ClientId,
                ClientName = dto.ClientName,
                SubTotal = 0,
                TaxAmount = 0,
                Total = 0
            };

            foreach (var l in dto.Lines)
            {
                var lineSubTotal = l.Quantity * l.UnitPrice;
                var lineTaxAmount = Math.Round(lineSubTotal * 0.21m, 2);
                var line = new SalesOrderLine
                {
                    ProductId = l.ProductId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    TaxRate = 21m,
                    TaxAmount = lineTaxAmount,
                };
                so.Lines.Add(line);
                _context.SalesOrderLines.Add(line);
                so.SubTotal += lineSubTotal;
                so.TaxAmount += lineTaxAmount;
            }

            so.Total = so.SubTotal + so.TaxAmount;

            _context.SalesOrders.Add(so);
            await _context.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(Get), new { id = so.Id }, so);
        }
    }
}
