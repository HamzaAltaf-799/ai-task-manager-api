using AITaskManager.API.Common;
using AITaskManager.API.DTOs.Tasks;
using AITaskManager.API.Interfaces;
using AITaskManager.API.Models;

namespace AITaskManager.API.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _repo;
    private readonly IAiService _ai;
    private readonly ICacheService _cache;
    private readonly ILogger<TaskService> _logger;

    public TaskService(ITaskRepository repo, IAiService ai, ICacheService cache, ILogger<TaskService> logger)
    {
        _repo   = repo;
        _ai     = ai;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<PagedResult<TaskResponse>> GetTasksAsync(string userId, TaskQueryParameters parameters)
    {
        var result = await _repo.GetAllAsync(userId, parameters);
        return new PagedResult<TaskResponse>
        {
            Items      = result.Items.Select(ToResponse),
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize
        };
    }

    public async Task<TaskResponse?> GetTaskByIdAsync(Guid id, string userId)
    {
        var cacheKey = $"task:{userId}:{id}";
        var cached = _cache.Get<TaskResponse>(cacheKey);
        if (cached is not null) return cached;

        var task = await _repo.GetByIdAsync(id, userId);
        if (task is null) return null;

        var response = ToResponse(task);
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));
        return response;
    }

    public async Task<TaskResponse> CreateTaskAsync(string userId, CreateTaskRequest request)
    {
        var task = new TaskItem
        {
            Title       = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority    = request.Priority,
            DueDate     = request.DueDate,
            Category    = request.Category?.Trim(),
            UserId      = userId
        };

        var created = await _repo.CreateAsync(task);
        _cache.RemoveByPrefix($"tasks:{userId}");
        _logger.LogInformation("Task {Id} created for user {UserId}", created.Id, userId);
        return ToResponse(created);
    }

    public async Task<TaskResponse?> UpdateTaskAsync(Guid id, string userId, UpdateTaskRequest request)
    {
        var task = await _repo.GetByIdAsync(id, userId);
        if (task is null) return null;

        if (request.Title    is not null)  task.Title       = request.Title.Trim();
        if (request.Description is not null) task.Description = request.Description.Trim();
        if (request.Priority.HasValue)     task.Priority    = request.Priority.Value;
        if (request.Status.HasValue)       task.Status      = request.Status.Value;
        if (request.DueDate.HasValue)      task.DueDate     = request.DueDate.Value;
        if (request.Category is not null)  task.Category    = request.Category.Trim();

        var updated = await _repo.UpdateAsync(task);
        _cache.Remove($"task:{userId}:{id}");
        _cache.RemoveByPrefix($"tasks:{userId}");
        return ToResponse(updated);
    }

    public async Task<bool> DeleteTaskAsync(Guid id, string userId)
    {
        var deleted = await _repo.DeleteAsync(id, userId);
        if (deleted)
        {
            _cache.Remove($"task:{userId}:{id}");
            _cache.RemoveByPrefix($"tasks:{userId}");
        }
        return deleted;
    }

    public async Task<AiSummaryResponse?> SummarizeTaskAsync(Guid id, string userId)
    {
        var task = await _repo.GetByIdAsync(id, userId);
        if (task is null) return null;

        // Fan out AI calls in parallel
        var summaryTask     = _ai.GenerateSummaryAsync(task.Title, task.Description);
        var priorityTask    = _ai.SuggestPriorityAsync(task.Title, task.Description);
        var suggestionsTask = _ai.GenerateSuggestionsAsync(task.Title, task.Description);

        await Task.WhenAll(summaryTask, priorityTask, suggestionsTask);

        var summary = await summaryTask;
        task.AiSummary = summary;
        await _repo.UpdateAsync(task);
        _cache.Remove($"task:{userId}:{id}");

        return new AiSummaryResponse(task.Id, summary, await priorityTask, await suggestionsTask);
    }

    private static TaskResponse ToResponse(TaskItem t) => new(
        t.Id, t.Title, t.Description, t.Priority, t.Status, t.Category, t.AiSummary,
        t.DueDate, t.CreatedAt, t.UpdatedAt, t.UserId,
        IsOverdue: t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != Models.TaskStatus.Completed
    );
}
