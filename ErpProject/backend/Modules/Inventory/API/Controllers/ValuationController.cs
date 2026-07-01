using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Application.Services;
using Erp.Modules.Inventory.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;
using Microsoft.Extensions.Configuration;

namespace Erp.Modules.Inventory.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/inventory/valuation")]
    [ApiVersion("1.0")]
    public class ValuationController : ControllerBase
    {
        private readonly IInventoryDbContext _context;
        private readonly IConfiguration _config;

        public ValuationController(IInventoryDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpGet]
        public async Task<IActionResult> Calculate([FromQuery] string? method, CancellationToken ct)
        {
            var valuationMethod = method ?? _config["Inventory:ValuationMethod"] ?? "PMP";
            var strategy = InventoryValuationService.CreateStrategy(valuationMethod);

            var products = await _context.InventoryProducts.AsNoTracking().ToListAsync(ct);
            var stocks = await _context.Stocks.AsNoTracking().ToListAsync(ct);
            var movements = await _context.StockMovements.AsNoTracking().ToListAsync(ct);

            var result = products.Select(p => {
                var productMovements = movements.Where(m => m.ProductId == p.Id).ToList();
                var unitCost = strategy.CalculateUnitCost(productMovements);
                var qty = stocks.Where(s => s.ProductId == p.Id).Sum(s => s.Quantity);
                return new {
                    id = p.Id,
                    name = p.Name,
                    quantity = qty,
                    unitCost = unitCost,
                    totalValue = qty * unitCost
                };
            }).ToList();

            return Ok(result);
        }
    }
}
