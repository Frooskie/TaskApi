using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TaskApi.Features.Tasks.Contracts;

namespace TaskApi.Infrastructure.RabbitMq;

/// <summary>
/// Asynchronous RabbitMQ publisher built on an unbounded in-memory channel.
/// Callers enqueue events without blocking; a background loop drains the channel
/// and publishes to RabbitMQ. Failures are logged and swallowed per contract.
/// </summary>
internal sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    : BackgroundService, IRabbitMqPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private readonly RabbitMqOptions _options = options.Value;
    private readonly Channel<TaskCompletedEvent> _queue = Channel.CreateUnbounded<TaskCompletedEvent>();

    private IConnection? _connection;
    private IChannel? _channel;
    
    public async Task PublishAsync(TaskCompletedEvent @event, CancellationToken ct = default)
        => await _queue.Writer.WriteAsync(@event, ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectWithRetryAsync(stoppingToken);

        try
        {
            await foreach (var @event in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await TryPublishAsync(@event, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown — fall through to drain.
        }
        finally
        {
            await DrainAndDisposeAsync();
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = ReconnectDelay
        };

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
                await _channel.ExchangeDeclareAsync(
                    exchange: _options.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: ct);

                logger.LogInformation(
                    "RabbitMQ connected to {Host}:{Port}; exchange '{Exchange}' declared",
                    _options.HostName,
                    _options.Port,
                    _options.ExchangeName);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "RabbitMQ unavailable at {Host}:{Port}; retrying in {Delay}s",
                    _options.HostName,
                    _options.Port,
                    ReconnectDelay.TotalSeconds);

                try
                {
                    await Task.Delay(ReconnectDelay, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task TryPublishAsync(TaskCompletedEvent @event, CancellationToken ct)
    {
        if (_channel is null)
        {
            logger.LogWarning(
                "RabbitMQ channel unavailable; dropping task.completed for {TaskId}",
                @event.TaskId);
            return;
        }

        try
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(@event, JsonOptions);
            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };

            await _channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: _options.RoutingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);

            logger.LogDebug(
                "Published task.completed for {TaskId} to {Exchange}/{RoutingKey}",
                @event.TaskId,
                _options.ExchangeName,
                _options.RoutingKey);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish task.completed for {TaskId}",
                @event.TaskId);
        }
    }

    private async Task DrainAndDisposeAsync()
    {
        _queue.Writer.TryComplete();

        using var drainCts = new CancellationTokenSource(DrainTimeout);
        try
        {
            await foreach (var @event in _queue.Reader.ReadAllAsync(drainCts.Token))
            {
                await TryPublishAsync(@event, drainCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "RabbitMQ drain timeout ({Timeout}s) exceeded; some events may not have been published",
                DrainTimeout.TotalSeconds);
        }

        await DisposeRabbitAsync();
    }

    private async ValueTask DisposeRabbitAsync()
    {
        if (_channel is not null)
        {
            try
            {
                await _channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error disposing RabbitMQ channel");
            }
            _channel = null;
        }

        if (_connection is not null)
        {
            try
            {
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error disposing RabbitMQ connection");
            }
            _connection = null;
        }
    }
}