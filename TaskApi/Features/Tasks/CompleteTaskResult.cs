using TaskApi.Features.Tasks.Contracts;

namespace TaskApi.Features.Tasks;

public readonly record struct CompleteTaskResult(
    CompleteTaskOutcome Outcome,
    TaskResponse? Task)
{
    public static CompleteTaskResult Success(TaskResponse task)
        => new(CompleteTaskOutcome.Success, task);

    public static readonly CompleteTaskResult NotFound
        = new(CompleteTaskOutcome.NotFound, null);

    public static readonly CompleteTaskResult Conflict
        = new(CompleteTaskOutcome.Conflict, null);
}