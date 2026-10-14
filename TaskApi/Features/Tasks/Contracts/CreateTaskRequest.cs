using System.ComponentModel.DataAnnotations;

namespace TaskApi.Features.Tasks.Contracts;

public sealed record CreateTaskRequest
{
    [Required]
    [StringLength(200)]
    public required string Title { get; init; }

    public Priority Priority { get; init; } = Priority.Medium;
}