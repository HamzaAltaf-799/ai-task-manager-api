namespace AITaskManager.API.Models;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public Models.TaskStatus Status { get; set; } = Models.TaskStatus.Todo;
    public string? Category { get; set; }
    public string? AiSummary { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
}

public enum TaskPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }

public enum TaskStatus { Todo = 0, InProgress = 1, Blocked = 2, Completed = 3, Archived = 4 }
