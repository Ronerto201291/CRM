using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class LeadsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICrmDbContext _crmCtx;
    private readonly ITenantContext _tenantContext;

    public LeadsController(IMediator mediator, ICrmDbContext crmCtx, ITenantContext tenantContext)
    {
        _mediator = mediator;
        _crmCtx = crmCtx;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetLeadsQuery
        {
            Search = search,
            Status = status,
            Page = page,
            PageSize = pageSize,
        }, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var lead = await _crmCtx.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null) return NotFound();
        return Ok(new
        {
            lead.Id, lead.Name, lead.Email, lead.Phone, lead.TaxId, lead.Address,
            lead.Status, lead.Source, lead.Notes, lead.ConvertedToClientId, lead.CreatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LeadDto dto, CancellationToken ct)
    {
        var companyId = _tenantContext.TenantId;
        if (!companyId.HasValue) return Unauthorized();

        var lead = new Lead
        {
            Id        = Guid.NewGuid(),
            CompanyId = companyId.Value,
            Name      = dto.Name ?? string.Empty,
            Email     = dto.Email ?? string.Empty,
            Phone     = dto.Phone ?? string.Empty,
            TaxId     = dto.TaxId ?? string.Empty,
            Address   = dto.Address ?? string.Empty,
            Status    = string.IsNullOrWhiteSpace(dto.Status) ? "New" : dto.Status,
            Source    = dto.Source ?? string.Empty,
            Notes     = dto.Notes ?? string.Empty,
        };

        _crmCtx.Leads.Add(lead);

        _crmCtx.ActivityLogs.Add(new ActivityLog
        {
            Id         = Guid.NewGuid(),
            CompanyId  = companyId.Value,
            EntityType = "Lead", EntityId = lead.Id,
            Action     = "Created",
            Description = $"Posible cliente '{lead.Name}' creado (Origen: {lead.Source})"
        });

        await _crmCtx.SaveChangesAsync(ct);
        return Created($"/api/leads/{lead.Id}", new
        {
            lead.Id, lead.Name, lead.Email, lead.Phone, lead.TaxId, lead.Address,
            lead.Status, lead.Source, lead.Notes, lead.ConvertedToClientId, lead.CreatedAt
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] LeadDto dto, CancellationToken ct)
    {
        var lead = await _crmCtx.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null) return NotFound();

        lead.Name    = dto.Name    ?? lead.Name;
        lead.Email   = dto.Email   ?? lead.Email;
        lead.Phone   = dto.Phone   ?? lead.Phone;
        lead.TaxId   = dto.TaxId   ?? lead.TaxId;
        lead.Address = dto.Address ?? lead.Address;
        lead.Status  = dto.Status  ?? lead.Status;
        lead.Source  = dto.Source  ?? lead.Source;
        lead.Notes   = dto.Notes   ?? lead.Notes;

        await _crmCtx.SaveChangesAsync(ct);
        return Ok(new { message = "Posible cliente actualizado" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var lead = await _crmCtx.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null) return NotFound();

        _crmCtx.Leads.Remove(lead);
        await _crmCtx.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Convierte un posible cliente en cliente registrado.
    /// Crea un Client con los datos del Lead y marca el Lead como "Won".
    /// </summary>
    [HttpPost("{id:guid}/convert-to-client")]
    public async Task<IActionResult> ConvertToClient(Guid id, CancellationToken ct)
    {
        var companyId = _tenantContext.TenantId;
        if (!companyId.HasValue) return Unauthorized();

        var lead = await _crmCtx.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null) return NotFound();

        if (lead.ConvertedToClientId.HasValue)
            return Conflict(new { message = "Este posible cliente ya fue convertido en cliente.", clientId = lead.ConvertedToClientId });

        // Crear el Client a partir de los datos del Lead
        var client = new Client
        {
            Id           = Guid.NewGuid(),
            CompanyId    = companyId.Value,
            Name         = lead.Name,
            TaxId        = lead.TaxId,
            Email        = lead.Email,
            Phone        = lead.Phone,
            Address      = lead.Address,
            CustomFields = "{}",
        };

        _crmCtx.Clients.Add(client);

        // Marcar el lead como convertido
        lead.Status             = "Won";
        lead.ConvertedToClientId = client.Id;

        _crmCtx.ActivityLogs.Add(new ActivityLog
        {
            Id          = Guid.NewGuid(),
            CompanyId   = companyId.Value,
            EntityType  = "Lead", EntityId = lead.Id,
            Action      = "ConvertedToClient",
            Description = $"Lead '{lead.Name}' convertido en cliente (ClientId: {client.Id})"
        });

        _crmCtx.ActivityLogs.Add(new ActivityLog
        {
            Id          = Guid.NewGuid(),
            CompanyId   = companyId.Value,
            EntityType  = "Client", EntityId = client.Id,
            Action      = "CreatedFromLead",
            Description = $"Cliente '{client.Name}' creado desde posible cliente (LeadId: {lead.Id})"
        });

        await _crmCtx.SaveChangesAsync(ct);

        return Ok(new
        {
            message  = "Posible cliente convertido en cliente correctamente.",
            clientId = client.Id,
            client   = new { client.Id, client.Name, client.TaxId, client.Email, client.Phone, client.Address }
        });
    }
}

public class LeadDto
{
    public string? Name    { get; set; }
    public string? Email   { get; set; }
    public string? Phone   { get; set; }
    public string? TaxId   { get; set; }
    public string? Address { get; set; }
    public string? Status  { get; set; }
    public string? Source  { get; set; }
    public string? Notes   { get; set; }
}
