using MediatR;

namespace Erp.Tests.TestSupport;

/// <summary>Graba las notificaciones publicadas en vez de despacharlas a handlers reales.</summary>
public sealed class FakePublisher : IPublisher
{
    public List<object> Published { get; } = [];

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        Published.Add(notification);
        return Task.CompletedTask;
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        Published.Add(notification!);
        return Task.CompletedTask;
    }
}
