using Microsoft.EntityFrameworkCore;
using TaskApi.Features.Tasks.Contracts;
using TaskApi.Infrastructure.RabbitMq;

namespace TaskApi.Features.Tasks;

/// <summary>
/// Application service orchestrating task operations.
/// </summary>
public sealed class TaskService(
    TaskDbContext db,
    IRabbitMqPublisher publisher,
    TimeProvider clock,
    ILogger<TaskService> logger)
{
    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken ct)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            IsCompleted = false,
            CreatedAt = clock.GetUtcNow(),
            CompletedAt = null,
            Priority = request.Priority
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Task {TaskId} created", task.Id);
        return ToResponse(task);
    }

    public async Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken ct)
    {
        return await db.Tasks
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TaskResponse(
                t.Id, t.Title, t.IsCompleted, t.CreatedAt, t.CompletedAt, t.Priority))
            .ToListAsync(ct);
    }

    public async Task<CompleteTaskResult> CompleteAsync(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (task is null)
        {
            return CompleteTaskResult.NotFound;
        }
        
        if (task.IsCompleted)
        {
            return CompleteTaskResult.Conflict;
        }

        task.IsCompleted = true;
        task.CompletedAt = clock.GetUtcNow();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CompleteTaskResult.Conflict;
        }
        
        await publisher.PublishAsync(
            new TaskCompletedEvent(task.Id, task.Title, task.CompletedAt.Value, task.Priority),
            ct);

        logger.LogInformation("Task {TaskId} completed", task.Id);
        return CompleteTaskResult.Success(ToResponse(task));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var affected = await db.Tasks
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(ct);

        if (affected > 0)
        {
            logger.LogInformation("Task {TaskId} deleted", id);
        }

        return affected > 0;
    }

    private static TaskResponse ToResponse(TaskItem t) => new(
        t.Id, t.Title, t.IsCompleted, t.CreatedAt, t.CompletedAt, t.Priority);
}