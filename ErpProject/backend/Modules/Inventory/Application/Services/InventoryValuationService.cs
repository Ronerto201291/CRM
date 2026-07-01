using Erp.Modules.Inventory.Domain.Entities;

namespace Erp.Modules.Inventory.Application.Services
{
    public interface IValuationStrategy
    {
        decimal CalculateUnitCost(List<StockMovement> movements);
    }

    public class PMPValuation : IValuationStrategy
    {
        public decimal CalculateUnitCost(List<StockMovement> movements)
        {
            var inboundTotal = movements.Where(m => m.Quantity > 0).Sum(m => m.Quantity * m.UnitCost);
            var inboundQty = movements.Where(m => m.Quantity > 0).Sum(m => m.Quantity);
            return inboundQty > 0 ? inboundTotal / inboundQty : 0m;
        }
    }

    public class FIFOValuation : IValuationStrategy
    {
        public decimal CalculateUnitCost(List<StockMovement> movements)
        {
            var inbound = movements.Where(m => m.Quantity > 0).OrderBy(m => m.CreatedAt).ToList();
            if (!inbound.Any()) return 0m;
            return inbound.First().UnitCost;
        }
    }

    public class InventoryValuationService
    {
        public static IValuationStrategy CreateStrategy(string method)
        {
            return method.ToUpper() switch
            {
                "PMP" => new PMPValuation(),
                "FIFO" => new FIFOValuation(),
                _ => new PMPValuation()
            };
        }
    }
}
