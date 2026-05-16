using System.ComponentModel.DataAnnotations;
using AITaskManager.API.Models;

namespace AITaskManager.API.DTOs.Tasks;

public class CreateTaskRequest
{
    [Required] [MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
    [MaxLength(100)] public string? Category { get; set; }
}

public class UpdateTaskRequest
{
    [MaxLength(200)] public string? Title { get; set; }
    [MaxLength(2000)] public string? Description { get; set; }
    public TaskPriority? Priority { get; set; }
    public Models.TaskStatus? Status { get; set; }
    public DateTime? DueDate { get; set; }
    [MaxLength(100)] public string? Category { get; set; }
}

public record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    TaskPriority Priority,
    Models.TaskStatus Status,
    string? Category,
    string? AiSummary,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string UserId,
    bool IsOverdue
);

public record AiSummaryResponse(
    Guid TaskId,
    string Summary,
    string SuggestedPriority,
    IEnumerable<string> Suggestions
);
