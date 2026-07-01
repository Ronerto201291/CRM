using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Erp.Infrastructure.Messaging;

/// <summary>
/// Manages the RabbitMQ connection lifecycle.
/// Singleton — one connection per application instance.
/// </summary>
public sealed class RabbitMqConnectionFactory : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConnectionFactory> _logger;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RabbitMqConnectionFactory(IConfiguration configuration, ILogger<RabbitMqConnectionFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection?.IsOpen == true)
            return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection?.IsOpen == true)
                return _connection;

            var factory = new ConnectionFactory
            {
                Uri = new Uri(_configuration["RabbitMQ:Uri"] ?? "amqp://guest:guest@localhost:5672/"),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60)
            };

            _connection = await factory.CreateConnectionAsync("erp-saas", ct);
            _logger.LogInformation("RabbitMQ connection established to {Uri}", factory.Uri);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }
}
