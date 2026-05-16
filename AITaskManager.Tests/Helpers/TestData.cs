using AITaskManager.API.Auth;
using AITaskManager.API.Models;

namespace AITaskManager.Tests.Helpers;

public static class TestData
{
    public static ApplicationUser CreateUser(string? id = null) => new()
    {
        Id           = id ?? Guid.NewGuid().ToString(),
        FirstName    = "Test",
        LastName     = "User",
        Email        = $"test-{Guid.NewGuid():N}@example.com",
        PasswordHash = PasswordHasher.Hash("Test@123!"),
        Role         = "User",
        IsActive     = true
    };

    public static TaskItem CreateTask(string userId, TaskPriority priority = TaskPriority.Medium) => new()
    {
        Id          = Guid.NewGuid(),
        Title       = "Test Task",
        Description = "A task created for testing.",
        Priority    = priority,
        Status      = AITaskManager.API.Models.TaskStatus.Todo,
        Category    = "Testing",
        DueDate     = DateTime.UtcNow.AddDays(7),
        UserId      = userId
    };
}
