namespace TaskApi.Features.Tasks;

public sealed class TaskItem
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Priority Priority { get; set; }

    /// <summary>
    /// Токен конкурентности (Postgres xmin). Меняется при каждом UPDATE.
    /// </summary>
    public uint RowVersion { get; set; }
}