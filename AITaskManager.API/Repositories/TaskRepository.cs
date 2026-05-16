using AITaskManager.API.Common;
using AITaskManager.API.Data;
using AITaskManager.API.Interfaces;
using AITaskManager.API.Models;

namespace AITaskManager.API.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly InMemoryDatabase _db;

    public TaskRepository(InMemoryDatabase db) => _db = db;

    public Task<TaskItem?> GetByIdAsync(Guid id, string userId)
    {
        _db.Tasks.TryGetValue(id, out var task);
        var result = (task?.UserId == userId) ? task : null;
        return Task.FromResult(result);
    }

    public Task<PagedResult<TaskItem>> GetAllAsync(string userId, TaskQueryParameters p)
    {
        var query = _db.Tasks.Values.Where(t => t.UserId == userId).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var s = p.Search.ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(s) ||
                (t.Description?.ToLower().Contains(s) ?? false));
        }

        if (p.Status.HasValue)    query = query.Where(t => t.Status   == p.Status.Value);
        if (p.Priority.HasValue)  query = query.Where(t => t.Priority == p.Priority.Value);
        if (!string.IsNullOrWhiteSpace(p.Category))
            query = query.Where(t => t.Category == p.Category);
        if (p.DueBefore.HasValue) query = query.Where(t => t.DueDate <= p.DueBefore.Value);
        if (p.DueAfter.HasValue)  query = query.Where(t => t.DueDate >= p.DueAfter.Value);

        query = p.SortBy.ToLower() switch
        {
            "title"     => p.SortDirection == "asc" ? query.OrderBy(t => t.Title)     : query.OrderByDescending(t => t.Title),
            "priority"  => p.SortDirection == "asc" ? query.OrderBy(t => t.Priority)  : query.OrderByDescending(t => t.Priority),
            "status"    => p.SortDirection == "asc" ? query.OrderBy(t => t.Status)    : query.OrderByDescending(t => t.Status),
            "duedate"   => p.SortDirection == "asc" ? query.OrderBy(t => t.DueDate)   : query.OrderByDescending(t => t.DueDate),
            "updatedat" => p.SortDirection == "asc" ? query.OrderBy(t => t.UpdatedAt) : query.OrderByDescending(t => t.UpdatedAt),
            _           => p.SortDirection == "asc" ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt),
        };

        var all    = query.ToList();
        var total  = all.Count;
        var paged  = all.Skip((p.Page - 1) * p.PageSize).Take(p.PageSize).ToList();

        return Task.FromResult(new PagedResult<TaskItem>
        {
            Items      = paged,
            TotalCount = total,
            Page       = p.Page,
            PageSize   = p.PageSize
        });
    }

    public Task<TaskItem> CreateAsync(TaskItem task)
    {
        _db.Tasks[task.Id] = task;
        return Task.FromResult(task);
    }

    public Task<TaskItem> UpdateAsync(TaskItem task)
    {
        task.UpdatedAt = DateTime.UtcNow;
        _db.Tasks[task.Id] = task;
        return Task.FromResult(task);
    }

    public Task<bool> DeleteAsync(Guid id, string userId)
    {
        if (!_db.Tasks.TryGetValue(id, out var task) || task.UserId != userId)
            return Task.FromResult(false);

        _db.Tasks.TryRemove(id, out _);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(Guid id, string userId)
    {
        var exists = _db.Tasks.TryGetValue(id, out var task) && task.UserId == userId;
        return Task.FromResult(exists);
    }
}
