using System.ComponentModel.DataAnnotations;

namespace TaskApi.Features.Tasks.Contracts;

// HACK: нет IValidatableObject для отсечения whitespace-only Title.
// [Required] валидирует IsNullOrEmpty, а не IsNullOrWhiteSpace — строка "   " проходит,
// после .Trim() в TaskService в БД попадает "".
public sealed record CreateTaskRequest
{
    [Required]
    [StringLength(200)]
    public required string Title { get; init; }

    public Priority Priority { get; init; } = Priority.Medium;
}