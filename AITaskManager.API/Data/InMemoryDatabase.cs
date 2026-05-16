using System.Collections.Concurrent;
using AITaskManager.API.Auth;
using AITaskManager.API.Models;

namespace AITaskManager.API.Data;

/// <summary>
/// Thread-safe in-memory data store. 
/// Replace with EF Core + PostgreSQL by swapping the repository implementations.
/// The service layer is completely decoupled from this via repository interfaces.
/// </summary>
public class InMemoryDatabase
{
    public ConcurrentDictionary<string, ApplicationUser> Users { get; } = new();
    public ConcurrentDictionary<Guid, TaskItem> Tasks { get; } = new();

    public void Seed()
    {
        var adminId = "admin-seed-001";
        var demoId  = "demo-seed-001";

        var admin = new ApplicationUser
        {
            Id           = adminId,
            FirstName    = "System",
            LastName     = "Admin",
            Email        = "admin@aitaskmanager.dev",
            PasswordHash = PasswordHasher.Hash("Admin@123!"),
            Role         = "Admin",
            CreatedAt    = DateTime.UtcNow
        };
        Users[admin.Email] = admin;

        var demo = new ApplicationUser
        {
            Id           = demoId,
            FirstName    = "Demo",
            LastName     = "User",
            Email        = "demo@aitaskmanager.dev",
            PasswordHash = PasswordHasher.Hash("Demo@123!"),
            Role         = "User",
            CreatedAt    = DateTime.UtcNow
        };
        Users[demo.Email] = demo;

        var seedTasks = new[]
        {
            new TaskItem
            {
                Id          = Guid.NewGuid(),
                Title       = "Set up CI/CD pipeline",
                Description = "Configure GitHub Actions for automated testing and deployment to Railway.",
                Priority    = TaskPriority.High,
                Status      = Models.TaskStatus.InProgress,
                Category    = "DevOps",
                DueDate     = DateTime.UtcNow.AddDays(3),
                UserId      = demoId
            },
            new TaskItem
            {
                Id          = Guid.NewGuid(),
                Title       = "Write API documentation",
                Description = "Document all endpoints using OpenAPI annotations and update the README.",
                Priority    = TaskPriority.Medium,
                Status      = Models.TaskStatus.Todo,
                Category    = "Documentation",
                DueDate     = DateTime.UtcNow.AddDays(7),
                UserId      = demoId
            },
            new TaskItem
            {
                Id          = Guid.NewGuid(),
                Title       = "Implement rate limiting",
                Description = "Add per-user rate limiting middleware to prevent API abuse.",
                Priority    = TaskPriority.Critical,
                Status      = Models.TaskStatus.Completed,
                Category    = "Security",
                DueDate     = DateTime.UtcNow.AddDays(-1),
                UserId      = demoId
            }
        };

        foreach (var t in seedTasks)
            Tasks[t.Id] = t;
    }
}
