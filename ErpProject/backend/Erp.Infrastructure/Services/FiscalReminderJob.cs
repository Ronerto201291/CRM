using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Hangfire job que envía recordatorios de obligaciones fiscales.
/// Ejecuta diariamente y envía email/SMS 7 días antes del deadline.
/// </summary>
public class FiscalReminderJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FiscalReminderJob> _log;

    public FiscalReminderJob(IServiceScopeFactory scopeFactory, ILogger<FiscalReminderJob> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var in7Days = now.AddDays(7);

        // Buscar todos los eventos pendientes cuyo recordatorio esté entre hoy y 7 días
        var pendingEvents = await ctx.FiscalEvents
            .Where(e => e.Status == "Pending"
                     && e.ReminderDate >= now.Date
                     && e.ReminderDate <= in7Days.Date)
            .AsNoTracking()
            .ToListAsync(ct);

        if (pendingEvents.Count == 0)
        {
            _log.LogDebug("No hay recordatorios fiscales pendientes para los próximos 7 días");
            return;
        }

        // Agrupar por company para enviar un solo email por empresa
        var eventsByCompany = pendingEvents
            .GroupBy(e => e.CompanyId)
            .ToList();

        foreach (var group in eventsByCompany)
        {
            try
            {
                var company = await ctx.Companies
                    .Where(c => c.Id == group.Key)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct);

                if (company == null) continue;

                // Obtener emails de admins de la empresa
                var adminEmails = await ctx.Users
                    .Where(u => u.CompanyId == group.Key && u.IsActive)
                    .Include(u => u.Role)
                    .Where(u => u.Role != null && u.Role.Name == "Admin")
                    .Select(u => u.Email)
                    .Take(5)
                    .ToListAsync(ct);

                if (adminEmails.Count == 0) continue;

                var eventsList = group.OrderBy(e => e.DeadlineDate).ToList();

                var subject = $"[ERP] Recordatorio fiscal: {eventsList.Count} obligaciones pendientes (7 días)";

                var body = $@"<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: auto;'>
<h2 style='color: #2c3e50;'>📅 Recordatorio Fiscal</h2>
<p>Estimado/a, le informamos de las siguientes obligaciones fiscales pendientes:</p>
<table style='width: 100%; border-collapse: collapse; margin-top: 16px;'>
<thead>
<tr style='background: #3498db; color: white;'>
  <th style='padding: 10px; text-align: left;'>Modelo</th>
  <th style='padding: 10px; text-align: left;'>Período</th>
  <th style='padding: 10px; text-align: right;'>Fecha límite</th>
  <th style='padding: 10px; text-align: right;'>Días</th>
</tr>
</thead>
<tbody>";

                foreach (var evt in eventsList)
                {
                    var daysLeft = (evt.DeadlineDate.Date - now.Date).Days;
                    var urgency = daysLeft <= 3
                        ? "style='color: #e74c3c; font-weight: bold;'"
                        : "style='color: #27ae60;'";
                    body += $@"<tr style='border-bottom: 1px solid #eee;'>
  <td style='padding: 8px;'>{System.Web.HttpUtility.HtmlEncode(evt.ModelName)}</td>
  <td style='padding: 8px;'>{(evt.Quarter.HasValue ? $"T{evt.Quarter}" : (evt.Month.HasValue ? $"Mes {evt.Month}" : "Anual"))}</td>
  <td style='padding: 8px; text-align: right;'>{evt.DeadlineDate:dd/MM/yyyy}</td>
  <td style='padding: 8px; text-align: right;' {urgency}>{daysLeft} días</td>
</tr>";
                }

                body += @"</tbody>
</table>
<p style='margin-top: 20px; font-size: 13px; color: #7f8c8d;'>
Por favor, acceda a su ERP para revisar y presentar los modelos correspondientes antes de la fecha límite.
</p>
</body>
</html>";

                foreach (var email in adminEmails)
                {
                    try
                    {
                        await emailService.SendAsync(to: email, subject: subject, htmlBody: body, ct: ct);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Error enviando recordatorio fiscal a {Email}", email);
                    }
                }

                // Marcar los eventos como Reminded
                using var updateScope = _scopeFactory.CreateScope();
                var updateCtx = updateScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var eventIds = eventsList.Select(e => e.Id).ToList();
                var eventsToUpdate = await updateCtx.FiscalEvents
                    .Where(e => eventIds.Contains(e.Id))
                    .ToListAsync(ct);
                foreach (var evt in eventsToUpdate)
                    evt.Status = "Reminded";
                await updateCtx.SaveChangesAsync(ct);

                _log.LogInformation("Enviados {Count} recordatorios fiscales a empresa {CompanyId}",
                    eventsList.Count, group.Key);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error enviando recordatorios para empresa {CompanyId}", group.Key);
            }
        }
    }
}
