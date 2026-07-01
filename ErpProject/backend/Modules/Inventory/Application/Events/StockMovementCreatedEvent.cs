using MediatR;

namespace Erp.Modules.Inventory.Application.Events;

// Broadcasted out from Inventory module to whoever needs it (e.g., Accounting)
public class StockMovementCreatedEvent : INotification
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    
    public Guid CompanyId { get; }
    public Guid StockMovementId { get; }
    public string MovementType { get; } // Purchase, Sale
    public decimal TotalCost { get; } // Quantity * UnitCost
    public Guid? ReferenceId { get; } // The Invoice or Expense ID
    
    public StockMovementCreatedEvent(Guid companyId, Guid stockMovementId, string movementType, decimal totalCost, Guid? referenceId)
    {
        CompanyId = companyId;
        StockMovementId = stockMovementId;
        MovementType = movementType;
        TotalCost = totalCost;
        ReferenceId = referenceId;
    }
}


