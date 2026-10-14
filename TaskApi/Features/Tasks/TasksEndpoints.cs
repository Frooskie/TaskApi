using Microsoft.AspNetCore.Http.HttpResults;
using MinimalApis.Extensions.Filters;
using TaskApi.Endpoints;
using TaskApi.Features.Tasks.Contracts;

namespace TaskApi.Features.Tasks;

internal sealed class TasksEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tasks")
            .WithTags("Tasks");

        group.MapPost("/", CreateAsync)
            .WithName("CreateTask")
            .WithParameterValidation();

        group.MapGet("/", GetAllAsync)
            .WithName("GetAllTasks");

        group.MapPut("/{id:guid}/complete", CompleteAsync)
            .WithName("CompleteTask");

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteTask");
    }

    private static async Task<Created<TaskResponse>> CreateAsync(
        CreateTaskRequest request,
        TaskService service,
        CancellationToken ct)
    {
        var response = await service.CreateAsync(request, ct);
        return TypedResults.Created($"/tasks/{response.Id}", response);
    }

    private static async Task<Ok<IReadOnlyList<TaskResponse>>> GetAllAsync(
        TaskService service,
        CancellationToken ct)
    {
        var tasks = await service.GetAllAsync(ct);
        return TypedResults.Ok(tasks);
    }

    private static async Task<Results<Ok<TaskResponse>, NotFound, Conflict>> CompleteAsync(
        Guid id,
        TaskService service,
        CancellationToken ct)
    {
        var result = await service.CompleteAsync(id, ct);

        return result switch
        {
            { Outcome: CompleteTaskOutcome.Success, Task: { } task } => TypedResults.Ok(task),
            { Outcome: CompleteTaskOutcome.NotFound } => TypedResults.NotFound(),
            _ => TypedResults.Conflict()
        };
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id,
        TaskService service,
        CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}