using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Commands;

public class CreateWarehouseResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
}

public class CreateWarehouseCommand : IRequest<CreateWarehouseResult>
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
}

public class UpdateWarehouseCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
}

public class SetWarehouseActiveCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}
