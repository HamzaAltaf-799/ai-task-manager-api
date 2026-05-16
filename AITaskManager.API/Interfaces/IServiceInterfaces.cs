using AITaskManager.API.DTOs.Auth;
using AITaskManager.API.DTOs.Tasks;
using AITaskManager.API.Common;
using AITaskManager.API.Models;

namespace AITaskManager.API.Interfaces;

public interface IAuthService
{
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
}

public interface ITaskService
{
    Task<PagedResult<TaskResponse>> GetTasksAsync(string userId, TaskQueryParameters parameters);
    Task<TaskResponse?> GetTaskByIdAsync(Guid id, string userId);
    Task<TaskResponse> CreateTaskAsync(string userId, CreateTaskRequest request);
    Task<TaskResponse?> UpdateTaskAsync(Guid id, string userId, UpdateTaskRequest request);
    Task<bool> DeleteTaskAsync(Guid id, string userId);
    Task<AiSummaryResponse?> SummarizeTaskAsync(Guid id, string userId);
}

public interface IAiService
{
    Task<string> GenerateSummaryAsync(string title, string? description);
    Task<string> SuggestPriorityAsync(string title, string? description);
    Task<IEnumerable<string>> GenerateSuggestionsAsync(string title, string? description);
}

public interface ICacheService
{
    T? Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan? expiry = null);
    void Remove(string key);
    void RemoveByPrefix(string prefix);
}
