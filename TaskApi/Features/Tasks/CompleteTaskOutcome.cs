namespace TaskApi.Features.Tasks;

public enum CompleteTaskOutcome
{
    Success = 0,
    NotFound = 1,
    Conflict = 2
}