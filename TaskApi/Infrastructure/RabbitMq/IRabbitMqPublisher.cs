using TaskApi.Features.Tasks.Contracts;

namespace TaskApi.Infrastructure.RabbitMq;

public interface IRabbitMqPublisher
{
    Task PublishAsync(TaskCompletedEvent @event, CancellationToken ct = default);
}