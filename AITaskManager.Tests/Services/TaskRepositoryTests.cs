using AITaskManager.API.Common;
using AITaskManager.API.Data;
using AITaskManager.API.Models;
using AITaskManager.API.Repositories;
using AITaskManager.Tests.Helpers;

namespace AITaskManager.Tests.Services;

public static class TaskRepositoryTests
{
    public static void RunAll()
    {
        Console.WriteLine("\n[TaskRepository]");
        CreateAsync_ShouldPersistTask();
        GetByIdAsync_ShouldReturnTask_ForOwner();
        GetByIdAsync_ShouldReturnNull_ForDifferentUser();
        GetAllAsync_ShouldRespectPagination();
        GetAllAsync_ShouldFilterByStatus();
        GetAllAsync_ShouldFilterByPriority();
        GetAllAsync_ShouldFilterBySearch();
        DeleteAsync_ShouldRemoveTask();
        DeleteAsync_ShouldReturnFalse_ForNonexistentTask();
        DeleteAsync_ShouldReturnFalse_ForWrongUser();
        UpdateAsync_ShouldUpdateTimestamp();
    }

    private static (InMemoryDatabase db, TaskRepository repo) Setup()
    {
        var db   = new InMemoryDatabase();
        var repo = new TaskRepository(db);
        return (db, repo);
    }

    private static void CreateAsync_ShouldPersistTask()
    {
        var (db, repo) = Setup();
        var userId = Guid.NewGuid().ToString();
        var task   = TestData.CreateTask(userId);

        var result = repo.CreateAsync(task).GetAwaiter().GetResult();

        Assert.NotNull(result, "CreateAsync: result");
        Assert.Equal(task.Id, result.Id, "CreateAsync: Id matches");
        Assert.True(db.Tasks.ContainsKey(task.Id), "CreateAsync: stored in db");
        Console.WriteLine("  ✅ CreateAsync persists task");
    }

    private static void GetByIdAsync_ShouldReturnTask_ForOwner()
    {
        var (_, repo) = Setup();
        var userId = Guid.NewGuid().ToString();
        var task   = TestData.CreateTask(userId);
        repo.CreateAsync(task).GetAwaiter().GetResult();

        var result = repo.GetByIdAsync(task.Id, userId).GetAwaiter().GetResult();

        Assert.NotNull(result, "GetById: owner can read own task");
        Assert.Equal(task.Id, result!.Id, "GetById: correct task returned");
        Console.WriteLine("  ✅ GetByIdAsync returns task for owner");
    }

    private static void GetByIdAsync_ShouldReturnNull_ForDifferentUser()
    {
        var (_, repo) = Setup();
        var ownerId    = Guid.NewGuid().ToString();
        var attackerId = Guid.NewGuid().ToString();
        var task       = TestData.CreateTask(ownerId);
        repo.CreateAsync(task).GetAwaiter().GetResult();

        var result = repo.GetByIdAsync(task.Id, attackerId).GetAwaiter().GetResult();

        Assert.Null(result, "GetById: attacker gets null for another user's task");
        Console.WriteLine("  ✅ GetByIdAsync returns null for wrong user (security)");
    }

    private static void GetAllAsync_ShouldRespectPagination()
    {
        var (_, repo) = Setup();
        var userId = Guid.NewGuid().ToString();

        for (int i = 0; i < 15; i++)
            repo.CreateAsync(TestData.CreateTask(userId)).GetAwaiter().GetResult();

        var result = repo.GetAllAsync(userId, new TaskQueryParameters { Page = 2, PageSize = 5 })
                         .GetAwaiter().GetResult();

        Assert.Equal(15, result.TotalCount, "Pagination: total count");
        Assert.Equal(3,  result.TotalPages, "Pagination: total pages");
        Assert.Count(result.Items, 5,        "Pagination: page size");
        Assert.True(result.HasNextPage,       "Pagination: has next page");
        Assert.True(result.HasPreviousPage,   "Pagination: has previous page");
        Console.WriteLine("  ✅ GetAllAsync paginates correctly");
    }

    private static void GetAllAsync_ShouldFilterByStatus()
    {
        var (_, repo) = Setup();
        var userId = Guid.NewGuid().ToString();

        var t1 = TestData.CreateTask(userId); t1.Status = AITaskManager.API.Models.TaskStatus.Completed;
        var t2 = TestData.CreateTask(userId); t2.Status = AITaskManager.API.Models.TaskStatus.InProgress;
        var t3 = TestData.CreateTask(userId); t3.Status = AITaskManager.API.Models.TaskStatus.InProgress;
        repo.CreateAsync(t1).GetAwaiter().GetResult();
        repo.CreateAsync(t2).GetAwaiter().GetResult();
        repo.CreateAsync(t3).GetAwaiter().GetResult();

        var result = repo.GetAllAsync(userId, new TaskQueryParameters { Status = AITaskManager.API.Models.TaskStatus.InProgress })
                         .GetAwaiter().GetResult();

        Assert.Equal(2, result.TotalCount, "FilterByStatus: count");
        Console.WriteLine("  ✅ GetAllAsync filters by status");
    }

    private static void GetAllAsync_ShouldFilterByPriority()
    {
        var (_, repo) = Setup();
        var userId = Guid.NewGuid().ToString();

        repo.CreateAsync(TestData.CreateTask(userId, TaskPriority.High)).GetAwaiter().GetResult();
        repo.CreateAsync(TestData.CreateTask(userId, TaskPriority.Low)).GetAwaiter().GetResult();

        var result = repo.GetAllAsync(userId, new TaskQueryParameters { Priority = TaskPriority.High })
                         .GetAwaiter().GetResult();

        Assert.Equal(1, result.TotalCount, "FilterByPriority: count");
        Console.WriteLine("  ✅ GetAllAsync filters by priority");
    }

    private static void GetAllAsync_ShouldFilterBySearch()
    {
        var (_, repo) = Setup();
        var userId = Guid.NewGuid().ToString();

        var t1 = TestData.CreateTask(userId); t1.Title = "Deploy to production";
        var t2 = TestData.CreateTask(userId); t2.Title = "Write documentation";
        repo.CreateAsync(t1).GetAwaiter().GetResult();
        repo.CreateAsync(t2).GetAwaiter().GetResult();

        var result = repo.GetAllAsync(userId, new TaskQueryParameters { Search = "deploy" })
                         .GetAwaiter().GetResult();

        Assert.Equal(1, result.TotalCount, "FilterBySearch: count");
        Console.WriteLine("  ✅ GetAllAsync filters by search term");
    }

    private static void DeleteAsync_ShouldRemoveTask()
    {
        var (db, repo) = Setup();
        var userId = Guid.NewGuid().ToString();
        var task   = TestData.CreateTask(userId);
        repo.CreateAsync(task).GetAwaiter().GetResult();

        var deleted = repo.DeleteAsync(task.Id, userId).GetAwaiter().GetResult();

        Assert.True(deleted, "DeleteAsync: returns true");
        Assert.False(db.Tasks.ContainsKey(task.Id), "DeleteAsync: removed from db");
        Console.WriteLine("  ✅ DeleteAsync removes task");
    }

    private static void DeleteAsync_ShouldReturnFalse_ForNonexistentTask()
    {
        var (_, repo) = Setup();
        var result = repo.DeleteAsync(Guid.NewGuid(), Guid.NewGuid().ToString())
                         .GetAwaiter().GetResult();
        Assert.False(result, "Delete: nonexistent returns false");
        Console.WriteLine("  ✅ DeleteAsync returns false for nonexistent task");
    }

    private static void DeleteAsync_ShouldReturnFalse_ForWrongUser()
    {
        var (db, repo) = Setup();
        var userId     = Guid.NewGuid().ToString();
        var task       = TestData.CreateTask(userId);
        repo.CreateAsync(task).GetAwaiter().GetResult();

        var deleted = repo.DeleteAsync(task.Id, "attacker-id").GetAwaiter().GetResult();

        Assert.False(deleted, "Delete: wrong user returns false");
        Assert.True(db.Tasks.ContainsKey(task.Id), "Delete: task still exists after failed delete");
        Console.WriteLine("  ✅ DeleteAsync returns false for wrong user (security)");
    }

    private static void UpdateAsync_ShouldUpdateTimestamp()
    {
        var (_, repo) = Setup();
        var userId = Guid.NewGuid().ToString();
        var task   = TestData.CreateTask(userId);
        var before = task.UpdatedAt;
        repo.CreateAsync(task).GetAwaiter().GetResult();

        Thread.Sleep(5); // Ensure time passes
        task.Title = "Updated Title";
        var updated = repo.UpdateAsync(task).GetAwaiter().GetResult();

        Assert.True(updated.UpdatedAt >= before, "UpdateAsync: UpdatedAt advanced");
        Assert.Equal("Updated Title", updated.Title, "UpdateAsync: title changed");
        Console.WriteLine("  ✅ UpdateAsync sets UpdatedAt timestamp");
    }
}
