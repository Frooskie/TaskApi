using System.ComponentModel.DataAnnotations;

namespace TaskApi.Infrastructure.RabbitMq;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public required string HostName { get; init; }

    [Range(1, 65535)]
    public required int Port { get; init; }

    [Required]
    public required string UserName { get; init; }

    [Required]
    public required string Password { get; init; }

    [Required]
    public required string ExchangeName { get; init; }

    [Required]
    public required string RoutingKey { get; init; }
}