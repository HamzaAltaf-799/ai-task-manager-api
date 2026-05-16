using AITaskManager.API.Common;
using AITaskManager.API.Data;
using AITaskManager.API.DTOs.Tasks;
using AITaskManager.API.Interfaces;
using AITaskManager.API.Models;
using AITaskManager.API.Repositories;
using AITaskManager.API.Services;
using AITaskManager.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace AITaskManager.Tests.Services;

public static class TaskServiceTests
{
    public static void RunAll()
    {
        Console.WriteLine("\n[TaskService]");
        CreateTask_ShouldReturnResponse_WithCorrectFields();
        CreateTask_ShouldInvalidateCache();
        GetTaskById_ShouldReturnFromCache_OnSecondCall();
        GetTaskById_ShouldReturnNull_ForMissingTask();
        UpdateTask_ShouldApplyPartialUpdate();
        UpdateTask_ShouldReturnNull_ForMissingTask();
        DeleteTask_ShouldReturnTrue_WhenExists();
        DeleteTask_ShouldReturnFalse_WhenMissing();
        SummarizeTask_ShouldPersistSummary();
        GetTasks_ShouldReturnPagedResult();
        GetTasks_ShouldIsolateByUser();
    }

    private static (TaskService svc, InMemoryDatabase db) Setup()
    {
        var db      = new InMemoryDatabase();
        var repo    = new TaskRepository(db);
        var cache   = new CacheService();
        var ai      = new StubAiService();
        var logger  = NullLogger<TaskService>.Instance;
        var svc     = new TaskService(repo, ai, cache, logger);
        return (svc, db);
    }

    private static void CreateTask_ShouldReturnResponse_WithCorrectFields()
    {
        var (svc, _) = Setup();
        var userId   = Guid.NewGuid().ToString();
        var req      = new CreateTaskRequest { Title = "Write tests", Priority = TaskPriority.High };

        var result = svc.CreateTaskAsync(userId, req).GetAwaiter().GetResult();

        Assert.NotNull(result, "CreateTask: result");
        Assert.Equal("Write tests", result.Title,    "CreateTask: title");
        Assert.Equal(TaskPriority.High, result.Priority, "CreateTask: priority");
        Assert.Equal(userId, result.UserId,           "CreateTask: userId");
        Assert.Equal(AITaskManager.API.Models.TaskStatus.Todo, result.Status, "CreateTask: default status");
        Console.WriteLine("  ✅ CreateTask returns correct response fields");
    }

    private static void CreateTask_ShouldInvalidateCache()
    {
        var (svc, db) = Setup();
        var userId    = Guid.NewGuid().ToString();

        // First task populates list
        svc.CreateTaskAsync(userId, new CreateTaskRequest { Title = "Task 1" }).GetAwaiter().GetResult();
        // Second task should invalidate any cached list
        svc.CreateTaskAsync(userId, new CreateTaskRequest { Title = "Task 2" }).GetAwaiter().GetResult();

        var tasks = svc.GetTasksAsync(userId, new TaskQueryParameters()).GetAwaiter().GetResult();
        Assert.Equal(2, tasks.TotalCount, "CreateTask: both tasks visible after cache invalidation");
        Console.WriteLine("  ✅ CreateTask invalidates list cache");
    }

    private static void GetTaskById_ShouldReturnFromCache_OnSecondCall()
    {
        var (svc, db) = Setup();
        var userId    = Guid.NewGuid().ToString();
        var req       = new CreateTaskRequest { Title = "Cached Task" };
        var created   = svc.CreateTaskAsync(userId, req).GetAwaiter().GetResult();

        var first  = svc.GetTaskByIdAsync(created.Id, userId).GetAwaiter().GetResult();
        // Mutate db directly — cache should serve stale (expected cache-aside behavior)
        db.Tasks[created.Id].Title = "MUTATED";
        var second = svc.GetTaskByIdAsync(created.Id, userId).GetAwaiter().GetResult();

        Assert.Equal("Cached Task", second!.Title, "GetTaskById: second call served from cache");
        Console.WriteLine("  ✅ GetTaskById serves from cache on repeat calls");
    }

    private static void GetTaskById_ShouldReturnNull_ForMissingTask()
    {
        var (svc, _) = Setup();
        var result   = svc.GetTaskByIdAsync(Guid.NewGuid(), "any-user").GetAwaiter().GetResult();
        Assert.Null(result, "GetTaskById: missing task returns null");
        Console.WriteLine("  ✅ GetTaskById returns null for nonexistent task");
    }

    private static void UpdateTask_ShouldApplyPartialUpdate()
    {
        var (svc, _) = Setup();
        var userId   = Guid.NewGuid().ToString();
        var created  = svc.CreateTaskAsync(userId, new CreateTaskRequest { Title = "Original" })
                          .GetAwaiter().GetResult();

        var update = new UpdateTaskRequest { Title = "Updated", Status = AITaskManager.API.Models.TaskStatus.InProgress };
        var result = svc.UpdateTaskAsync(created.Id, userId, update).GetAwaiter().GetResult();

        Assert.NotNull(result, "UpdateTask: result");
        Assert.Equal("Updated", result!.Title, "UpdateTask: title changed");
        Assert.Equal(AITaskManager.API.Models.TaskStatus.InProgress, result.Status, "UpdateTask: status changed");
        Console.WriteLine("  ✅ UpdateTask applies partial fields correctly");
    }

    private static void UpdateTask_ShouldReturnNull_ForMissingTask()
    {
        var (svc, _) = Setup();
        var result   = svc.UpdateTaskAsync(Guid.NewGuid(), "user", new UpdateTaskRequest { Title = "x" })
                          .GetAwaiter().GetResult();
        Assert.Null(result, "UpdateTask: missing returns null");
        Console.WriteLine("  ✅ UpdateTask returns null for nonexistent task");
    }

    private static void DeleteTask_ShouldReturnTrue_WhenExists()
    {
        var (svc, _) = Setup();
        var userId   = Guid.NewGuid().ToString();
        var created  = svc.CreateTaskAsync(userId, new CreateTaskRequest { Title = "Delete Me" })
                          .GetAwaiter().GetResult();

        var deleted = svc.DeleteTaskAsync(created.Id, userId).GetAwaiter().GetResult();
        Assert.True(deleted, "DeleteTask: returns true");

        var gone = svc.GetTaskByIdAsync(created.Id, userId).GetAwaiter().GetResult();
        Assert.Null(gone, "DeleteTask: task no longer retrievable");
        Console.WriteLine("  ✅ DeleteTask removes task and clears cache");
    }

    private static void DeleteTask_ShouldReturnFalse_WhenMissing()
    {
        var (svc, _) = Setup();
        var result   = svc.DeleteTaskAsync(Guid.NewGuid(), "user").GetAwaiter().GetResult();
        Assert.False(result, "DeleteTask: missing returns false");
        Console.WriteLine("  ✅ DeleteTask returns false for nonexistent task");
    }

    private static void SummarizeTask_ShouldPersistSummary()
    {
        var (svc, db) = Setup();
        var userId    = Guid.NewGuid().ToString();
        var created   = svc.CreateTaskAsync(userId, new CreateTaskRequest
        {
            Title       = "Ship the feature",
            Description = "Deploy the payment flow to production."
        }).GetAwaiter().GetResult();

        var summary = svc.SummarizeTaskAsync(created.Id, userId).GetAwaiter().GetResult();

        Assert.NotNull(summary, "SummarizeTask: result");
        Assert.NotNull(summary!.Summary, "SummarizeTask: summary text");
        Assert.NotNull(summary.SuggestedPriority, "SummarizeTask: priority");
        Assert.True(summary.Suggestions.Any(), "SummarizeTask: at least one suggestion");

        // Verify persisted back to the task
        var persisted = db.Tasks[created.Id];
        Assert.NotNull(persisted.AiSummary, "SummarizeTask: summary persisted to task");
        Console.WriteLine("  ✅ SummarizeTask returns AI insights and persists summary");
    }

    private static void GetTasks_ShouldReturnPagedResult()
    {
        var (svc, _) = Setup();
        var userId   = Guid.NewGuid().ToString();

        for (int i = 1; i <= 12; i++)
            svc.CreateTaskAsync(userId, new CreateTaskRequest { Title = $"Task {i}" })
               .GetAwaiter().GetResult();

        var result = svc.GetTasksAsync(userId, new TaskQueryParameters { Page = 1, PageSize = 5 })
                        .GetAwaiter().GetResult();

        Assert.Equal(12, result.TotalCount, "GetTasks: total count");
        Assert.Count(result.Items, 5, "GetTasks: page size respected");
        Assert.Equal(3, result.TotalPages, "GetTasks: total pages");
        Console.WriteLine("  ✅ GetTasks returns correct paged result");
    }

    private static void GetTasks_ShouldIsolateByUser()
    {
        var (svc, _) = Setup();
        var user1    = Guid.NewGuid().ToString();
        var user2    = Guid.NewGuid().ToString();

        svc.CreateTaskAsync(user1, new CreateTaskRequest { Title = "User1 Task" }).GetAwaiter().GetResult();
        svc.CreateTaskAsync(user2, new CreateTaskRequest { Title = "User2 Task" }).GetAwaiter().GetResult();

        var user1Tasks = svc.GetTasksAsync(user1, new TaskQueryParameters()).GetAwaiter().GetResult();
        var user2Tasks = svc.GetTasksAsync(user2, new TaskQueryParameters()).GetAwaiter().GetResult();

        Assert.Equal(1, user1Tasks.TotalCount, "Isolation: user1 sees only own tasks");
        Assert.Equal(1, user2Tasks.TotalCount, "Isolation: user2 sees only own tasks");
        Console.WriteLine("  ✅ GetTasks isolates tasks by user");
    }
}
