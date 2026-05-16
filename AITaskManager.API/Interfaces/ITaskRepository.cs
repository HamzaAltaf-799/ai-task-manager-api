using AITaskManager.API.Common;
using AITaskManager.API.Models;

namespace AITaskManager.API.Interfaces;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, string userId);
    Task<PagedResult<TaskItem>> GetAllAsync(string userId, TaskQueryParameters parameters);
    Task<TaskItem> CreateAsync(TaskItem task);
    Task<TaskItem> UpdateAsync(TaskItem task);
    Task<bool> DeleteAsync(Guid id, string userId);
    Task<bool> ExistsAsync(Guid id, string userId);
}
