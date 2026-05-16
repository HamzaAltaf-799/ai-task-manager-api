using System.Security.Claims;
using AITaskManager.API.Auth;
using AITaskManager.API.Common;
using AITaskManager.API.DTOs.Tasks;
using AITaskManager.API.Interfaces;
using AITaskManager.API.Validators;
using Microsoft.AspNetCore.Mvc;

namespace AITaskManager.API.Controllers;

[ApiController]
[Route("api/tasks")]
[Produces("application/json")]
[RequireAuth]
public class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ITaskService tasks, ILogger<TasksController> logger)
    {
        _tasks  = tasks;
        _logger = logger;
    }

    /// <summary>Get all tasks for the current user. Supports filtering, sorting, and pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks([FromQuery] TaskQueryParameters parameters)
    {
        var result = await _tasks.GetTasksAsync(UserId, parameters);
        return Ok(ApiResponse<PagedResult<TaskResponse>>.Ok(result));
    }

    /// <summary>Get a specific task by its ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTask(Guid id)
    {
        var task = await _tasks.GetTaskByIdAsync(id, UserId);
        return task is null
            ? NotFound(ApiResponse<object>.Fail("Task not found.", 404))
            : Ok(ApiResponse<TaskResponse>.Ok(task));
    }

    /// <summary>Create a new task.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TaskResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        var errors = RequestValidator.ValidateCreateTask(request);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", errors: errors));

        var task = await _tasks.CreateTaskAsync(UserId, request);
        return CreatedAtAction(nameof(GetTask), new { id = task.Id },
            ApiResponse<TaskResponse>.Created(task));
    }

    /// <summary>Update an existing task (partial update — only supplied fields are changed).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTask(Guid id, [FromBody] UpdateTaskRequest request)
    {
        var errors = RequestValidator.Validate(request);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", errors: errors));

        var updated = await _tasks.UpdateTaskAsync(id, UserId, request);
        return updated is null
            ? NotFound(ApiResponse<object>.Fail("Task not found.", 404))
            : Ok(ApiResponse<TaskResponse>.Ok(updated, "Task updated."));
    }

    /// <summary>Delete a task permanently.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        var deleted = await _tasks.DeleteTaskAsync(id, UserId);
        return deleted ? NoContent() : NotFound(ApiResponse<object>.Fail("Task not found.", 404));
    }

    /// <summary>
    /// Generate an AI-powered summary, priority recommendation, and productivity suggestions
    /// for the specified task. The summary is also persisted back to the task record.
    /// </summary>
    [HttpPost("{id:guid}/summarize")]
    [ProducesResponseType(typeof(ApiResponse<AiSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SummarizeTask(Guid id)
    {
        try
        {
            var result = await _tasks.SummarizeTaskAsync(id, UserId);
            return result is null
                ? NotFound(ApiResponse<object>.Fail("Task not found.", 404))
                : Ok(ApiResponse<AiSummaryResponse>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "AI summarization failed for task {Id}", id);
            return StatusCode(503, ApiResponse<object>.Fail("AI service temporarily unavailable.", 503));
        }
    }

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User identity missing from token.");
}
