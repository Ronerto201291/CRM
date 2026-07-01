using Erp.Application.Common.Events;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Erp.Infrastructure.Messaging;

/// <summary>
/// Consumes domain events from RabbitMQ and dispatches via MediatR.
/// One queue per event type, bound to the erp.events topic exchange.
/// Implements manual ack — messages are only acked after successful processing.
/// </summary>
public class ErpEventConsumerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly ILogger<ErpEventConsumerService> _logger;

    private static readonly Dictionary<string, (string queue, string routingKey, Type eventType)> Consumers = new()
    {
        ["invoice.approved"] = ("erp.invoice.approved", "invoice.approved", typeof(InvoiceApprovedEvent)),
        ["expense.approved"] = ("erp.expense.approved", "expense.approved", typeof(ExpenseApprovedEvent)),
    };

    public ErpEventConsumerService(
        IServiceProvider services,
        RabbitMqConnectionFactory connectionFactory,
        ILogger<ErpEventConsumerService> logger)
    {
        _services = services;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ErpEventConsumerService starting — consuming from RabbitMQ");

        try
        {
            var connection = await _connectionFactory.GetConnectionAsync(stoppingToken);
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Set QoS: process one message at a time per consumer
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

            // Declare exchange and queues
            await channel.ExchangeDeclareAsync(
                exchange: RabbitMqMessageBus.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            foreach (var (key, (queueName, bindingKey, _)) in Consumers)
            {
                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueBindAsync(
                    queue: queueName,
                    exchange: RabbitMqMessageBus.ExchangeName,
                    routingKey: bindingKey,
                    cancellationToken: stoppingToken);

                var eventType = Consumers[key].eventType;
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                    await HandleMessageAsync(channel, ea, eventType, stoppingToken);

                await channel.BasicConsumeAsync(
                    queue: queueName,
                    autoAck: false,   // manual ack for reliability
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("Consuming queue '{Queue}' (routing key: {Key})", queueName, bindingKey);
            }

            // Keep running until cancellation
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ErpEventConsumerService stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in ErpEventConsumerService");
            throw;
        }
    }

    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs ea, Type eventType, CancellationToken ct)
    {
        var messageId = ea.BasicProperties.MessageId ?? "unknown";
        try
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(body);
            if (envelope == null)
            {
                _logger.LogWarning("Failed to deserialize envelope for message {MessageId}", messageId);
                await channel.BasicNackAsync(ea.DeliveryTag, false, false, ct); // dead-letter
                return;
            }

            var domainEvent = (INotification?)JsonSerializer.Deserialize(envelope.Payload, eventType);
            if (domainEvent == null)
            {
                _logger.LogWarning("Failed to deserialize payload as {EventType}", eventType.Name);
                await channel.BasicNackAsync(ea.DeliveryTag, false, false, ct);
                return;
            }

            using var scope = _services.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Publish(domainEvent, ct);

            await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            _logger.LogDebug("Processed {EventType} from RabbitMQ (messageId={MessageId})", eventType.Name, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RabbitMQ message {MessageId} ({EventType})", messageId, eventType.Name);
            // Requeue for retry (up to RabbitMQ retry policy)
            await channel.BasicNackAsync(ea.DeliveryTag, false, true, ct);
        }
    }
}
