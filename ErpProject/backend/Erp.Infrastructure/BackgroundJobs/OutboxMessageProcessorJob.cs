using Hangfire;
using Erp.Infrastructure.Data;
using Erp.Domain.Entities.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Erp.Infrastructure.BackgroundJobs;

/// <summary>
/// Procesa los eventos del Outbox en background.
/// Garantiza "At-Least-Once" delivery incluso si el servidor se apaga.
/// </summary>
public class OutboxMessageProcessorJob
{
    private readonly ErpDbContext _context;
    private readonly IPublisher _publisher;
    private readonly ILogger<OutboxMessageProcessorJob> _logger;

    public OutboxMessageProcessorJob(
        ErpDbContext context,
        IPublisher publisher,
        ILogger<OutboxMessageProcessorJob> logger)
    {
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessOutboxMessagesAsync()
    {
        _logger.LogInformation("Iniciando procesamiento de OutboxMessages...");

        var outboxMessages = await _context.OutboxMessages
            .Where(om => om.Status == OutboxMessageStatus.Pending)
            .OrderBy(om => om.CreatedAt)
            .Take(100)
            .ToListAsync();

        if (outboxMessages.Count == 0)
        {
            _logger.LogDebug("No hay OutboxMessages pendientes");
            return;
        }

        _logger.LogInformation("Encontrados {Count} mensajes en el Outbox", outboxMessages.Count);

        foreach (var outboxMessage in outboxMessages)
        {
            try
            {
                var eventType = Type.GetType(outboxMessage.EventType);
                if (eventType == null)
                {
                    _logger.LogError("Tipo de evento no encontrado: {EventType}", outboxMessage.EventType);
                    outboxMessage.RetryCount++;
                    if (outboxMessage.RetryCount >= 5)
                        outboxMessage.Status = OutboxMessageStatus.Failed;
                    continue;
                }

                var @event = JsonSerializer.Deserialize(outboxMessage.Payload, eventType);
                if (@event == null)
                {
                    _logger.LogError("No se pudo deserializar evento {EventId}", outboxMessage.Id);
                    outboxMessage.Status = OutboxMessageStatus.Failed;
                    continue;
                }

                await _publisher.Publish(@event);

                outboxMessage.ProcessedAt = DateTime.UtcNow;
                outboxMessage.Status = OutboxMessageStatus.Processed;
                _context.OutboxMessages.Update(outboxMessage);

                _logger.LogInformation("Evento {EventId} procesado exitosamente", outboxMessage.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando OutboxMessage {EventId}", outboxMessage.Id);
                outboxMessage.RetryCount++;
                outboxMessage.Error = ex.Message;
                if (outboxMessage.RetryCount >= 5)
                {
                    outboxMessage.Status = OutboxMessageStatus.Failed;
                    _logger.LogError("OutboxMessage {EventId} falló después de 5 reintentos", outboxMessage.Id);
                }
                _context.OutboxMessages.Update(outboxMessage);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Procesamiento de OutboxMessages completado");
    }
}
