using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Asp.Versioning;

namespace Erp.Modules.Inventory.Api.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/inventory/valuation")]
    [ApiVersion("1.0")]
    public class ValuationController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IConfiguration _config;

        public ValuationController(IMediator mediator, IConfiguration config)
        {
            _mediator = mediator;
            _config = config;
        }

        [HttpGet]
        public async Task<IActionResult> Calculate([FromQuery] string? method, CancellationToken ct)
        {
            var valuationMethod = method ?? _config["Inventory:ValuationMethod"] ?? "PMP";
            var result = await _mediator.Send(new GetInventoryValuationQuery { Method = valuationMethod }, ct);
            return Ok(result.Select(v => new
            {
                id = v.Id,
                name = v.Name,
                quantity = v.Quantity,
                unitCost = v.UnitCost,
                totalValue = v.TotalValue,
            }));
        }
    }
}
