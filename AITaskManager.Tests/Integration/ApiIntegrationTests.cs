using AITaskManager.API.Configurations;
using AITaskManager.API.Data;
using AITaskManager.API.DTOs.Auth;
using AITaskManager.API.DTOs.Tasks;
using AITaskManager.API.Auth;
using AITaskManager.API.Models;
using AITaskManager.API.Repositories;
using AITaskManager.API.Services;
using AITaskManager.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AITaskManager.Tests.Integration;

/// <summary>
/// Full service-stack integration tests.
/// Exercises Auth → Task → AI → Cache in one end-to-end flow,
/// identical to what a real HTTP request triggers after controller delegation.
/// </summary>
public static class ApiIntegrationTests
{
    public static async Task RunAllAsync()
    {
        Console.WriteLine("\n[Integration — Full Service Stack]");

        await FullUserLifecycle_RegisterLoginCreateTaskSummarize();
        await MultiUser_IsolationEnforced();
        await CacheInvalidation_OnMutation();
        await PaginationAndFiltering_EndToEnd();
    }

    private static (AuthService auth, TaskService tasks, InMemoryDatabase db) BuildStack()
    {
        var db      = new InMemoryDatabase();
        db.Seed();

        var userRepo = new UserRepository(db);
        var taskRepo = new TaskRepository(db);
        var cache    = new CacheService();
        var ai       = new StubAiService();

        var jwtOpts = Options.Create(new JwtSettings
        {
            Secret        = "integration-test-secret-32-chars!!",
            Issuer        = "AITaskManager.API",
            Audience      = "AITaskManager.Clients",
            ExpiryMinutes = 60
        });
        var jwt = new JwtTokenService(jwtOpts, NullLogger<JwtTokenService>.Instance);

        var authSvc = new AuthService(userRepo, jwt, NullLogger<AuthService>.Instance);
        var taskSvc = new TaskService(taskRepo, ai, cache, NullLogger<TaskService>.Instance);

        return (authSvc, taskSvc, db);
    }

    private static async Task FullUserLifecycle_RegisterLoginCreateTaskSummarize()
    {
        var (auth, tasks, db) = BuildStack();

        // 1. Register
        var regResult = await auth.RegisterAsync(new RegisterRequest
        {
            FirstName = "Alice", LastName = "Smith",
            Email     = "alice@example.com",
            Password  = "Alice@Secure1"
        });
        Assert.NotNull(regResult, "Lifecycle: register returns result");
        Assert.NotNull(regResult!.AccessToken, "Lifecycle: token issued on register");
        var userId = regResult.User.Id;
        Console.WriteLine("  ✅ Register → token issued");

        // 2. Login
        var loginResult = await auth.LoginAsync(new LoginRequest
        {
            Email    = "alice@example.com",
            Password = "Alice@Secure1"
        });
        Assert.NotNull(loginResult, "Lifecycle: login succeeds");
        Assert.Equal(userId, loginResult!.User.Id, "Lifecycle: same user on login");
        Console.WriteLine("  ✅ Login → same user id");

        // 3. Create task
        var task = await tasks.CreateTaskAsync(userId, new CreateTaskRequest
        {
            Title       = "Ship the new feature",
            Description = "Deploy payment flow to prod.",
            Priority    = TaskPriority.Critical,
            Category    = "Engineering"
        });
        Assert.NotNull(task, "Lifecycle: task created");
        Assert.Equal(userId, task.UserId, "Lifecycle: task owned by user");
        Assert.Equal(API.Models.TaskStatus.Todo, task.Status, "Lifecycle: default status");
        Console.WriteLine("  ✅ CreateTask → correct owner and status");

        // 4. Update status
        var updated = await tasks.UpdateTaskAsync(task.Id, userId, new UpdateTaskRequest
        {
            Status = API.Models.TaskStatus.InProgress
        });
        Assert.Equal(API.Models.TaskStatus.InProgress, updated!.Status, "Lifecycle: status updated");
        Console.WriteLine("  ✅ UpdateTask → status changed to InProgress");

        // 5. AI summarize
        var summary = await tasks.SummarizeTaskAsync(task.Id, userId);
        Assert.NotNull(summary, "Lifecycle: summary returned");
        Assert.NotNull(summary!.Summary, "Lifecycle: summary text present");
        Assert.True(summary.Suggestions.Count() >= 3, "Lifecycle: 3 suggestions");
        Assert.NotNull(db.Tasks[task.Id].AiSummary, "Lifecycle: summary persisted to task");
        Console.WriteLine("  ✅ SummarizeTask → AI data returned and persisted");

        // 6. Delete
        var deleted = await tasks.DeleteTaskAsync(task.Id, userId);
        Assert.True(deleted, "Lifecycle: delete succeeds");
        var gone = await tasks.GetTaskByIdAsync(task.Id, userId);
        Assert.Null(gone, "Lifecycle: task gone after delete");
        Console.WriteLine("  ✅ DeleteTask → task removed");
    }

    private static async Task MultiUser_IsolationEnforced()
    {
        var (auth, tasks, _) = BuildStack();

        var alice = await auth.RegisterAsync(new RegisterRequest
        {
            FirstName = "Alice", LastName = "A", Email = "alice2@x.com", Password = "Alice@1234"
        });
        var bob = await auth.RegisterAsync(new RegisterRequest
        {
            FirstName = "Bob", LastName = "B", Email = "bob@x.com", Password = "Bob@12345"
        });

        var aliceTask = await tasks.CreateTaskAsync(alice!.User.Id, new CreateTaskRequest { Title = "Alice's secret" });
        var bobTask   = await tasks.CreateTaskAsync(bob!.User.Id,   new CreateTaskRequest { Title = "Bob's secret" });

        // Bob cannot read Alice's task
        var stolen = await tasks.GetTaskByIdAsync(aliceTask.Id, bob.User.Id);
        Assert.Null(stolen, "Isolation: Bob cannot read Alice's task");

        // Bob cannot delete Alice's task
        var badDelete = await tasks.DeleteTaskAsync(aliceTask.Id, bob.User.Id);
        Assert.False(badDelete, "Isolation: Bob cannot delete Alice's task");

        // Alice's task still exists
        var aliceStillHasIt = await tasks.GetTaskByIdAsync(aliceTask.Id, alice.User.Id);
        Assert.NotNull(aliceStillHasIt, "Isolation: Alice's task intact after Bob's failed delete");

        Console.WriteLine("  ✅ Cross-user isolation enforced at service layer");
    }

    private static async Task CacheInvalidation_OnMutation()
    {
        var (auth, tasks, db) = BuildStack();
        var user = await auth.RegisterAsync(new RegisterRequest
        {
            FirstName = "Cache", LastName = "Test", Email = "cache@test.com", Password = "Cache@1234"
        });
        var uid = user!.User.Id;

        var task = await tasks.CreateTaskAsync(uid, new CreateTaskRequest { Title = "Original" });

        // Warm the cache
        var cached = await tasks.GetTaskByIdAsync(task.Id, uid);
        Assert.Equal("Original", cached!.Title, "Cache: initial title");

        // Mutate via service (should bust cache)
        await tasks.UpdateTaskAsync(task.Id, uid, new UpdateTaskRequest { Title = "Mutated" });

        // Should now read the updated value
        var fresh = await tasks.GetTaskByIdAsync(task.Id, uid);
        Assert.Equal("Mutated", fresh!.Title, "Cache: stale entry replaced after update");

        Console.WriteLine("  ✅ Cache invalidation works after mutation");
    }

    private static async Task PaginationAndFiltering_EndToEnd()
    {
        var (auth, tasks, _) = BuildStack();
        var user = await auth.RegisterAsync(new RegisterRequest
        {
            FirstName = "Page", LastName = "Test", Email = "page@test.com", Password = "Page@1234"
        });
        var uid = user!.User.Id;

        // Create 12 tasks with mixed priorities
        for (int i = 1; i <= 12; i++)
        {
            await tasks.CreateTaskAsync(uid, new CreateTaskRequest
            {
                Title    = $"Task {i:D2}",
                Priority = i <= 4 ? TaskPriority.High : i <= 8 ? TaskPriority.Medium : TaskPriority.Low
            });
        }

        // Page 1 of 5
        var p1 = await tasks.GetTasksAsync(uid, new AITaskManager.API.Common.TaskQueryParameters { Page = 1, PageSize = 5 });
        Assert.Equal(12, p1.TotalCount, "Pagination: total 12");
        Assert.Equal(5,  p1.Items.Count(), "Pagination: page 1 has 5");
        Assert.True(p1.HasNextPage,        "Pagination: has next page");

        // Page 3 (last, 2 items)
        var p3 = await tasks.GetTasksAsync(uid, new AITaskManager.API.Common.TaskQueryParameters { Page = 3, PageSize = 5 });
        Assert.Equal(2, p3.Items.Count(), "Pagination: last page has 2");
        Assert.False(p3.HasNextPage,       "Pagination: no next page on last");

        // Filter High priority
        var high = await tasks.GetTasksAsync(uid, new AITaskManager.API.Common.TaskQueryParameters
        {
            Priority = TaskPriority.High
        });
        Assert.Equal(4, high.TotalCount, "Filter: 4 high priority tasks");

        Console.WriteLine("  ✅ Pagination and filtering work end-to-end");
    }
}
