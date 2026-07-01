using Erp.Modules.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Inventory.Application.Interfaces;

public interface IInventoryDbContext
{
    DbSet<Product> InventoryProducts { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Stock> Stocks { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Lot> Lots { get; }
    DbSet<SerialNumber> SerialNumbers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
