using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkloadProductivity.DTOs;
using WorkloadProductivity.Models;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;

    public TasksController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Create a new TaskItem.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TaskItemResponse>> CreateTaskAsync(
        [FromBody] CreateTaskItemRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Optional: input normalization/guards
        var title = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest("Title cannot be empty or whitespace.");

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            EstimatedHours = request.EstimatedHours,
            CreatedAt = DateTime.SpecifyKind(request.CreatedAt, DateTimeKind.Utc),
            UserId = request.UserId ?? Guid.Empty // set appropriately if required
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);

        // Compute aggregate (on-demand)
        var totalHours = await _db.WorkSessions
            .Where(ws => ws.TaskItemId == task.Id)
            .SumAsync(ws => (double?)ws.HoursSpent, ct) ?? 0.0;

        var response = new TaskItemResponse
        {
            Id = task.Id,
            Title = task.Title,
            EstimatedHours = task.EstimatedHours,
            CreatedAt = task.CreatedAt,
            UserId = task.UserId,
            TotalHoursSpent = totalHours
        };

        return CreatedAtAction("GetTaskById", new { id = task.Id }, response);
    }

    /// <summary>
    /// Get a TaskItem by id with aggregates.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskItemResponse>> GetTaskByIdAsync(Guid id, CancellationToken ct)
    {
        var task = await _db.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (task == null)
            return NotFound();

        var totalHours = await _db.WorkSessions
            .Where(ws => ws.TaskItemId == id)
            .SumAsync(ws => (double?)ws.HoursSpent, ct) ?? 0.0;

        return Ok(new TaskItemResponse
        {
            Id = task.Id,
            Title = task.Title,
            EstimatedHours = task.EstimatedHours,
            CreatedAt = task.CreatedAt,
            UserId = task.UserId,
            TotalHoursSpent = totalHours
        });
    }

    /// <summary>
    /// Create a WorkSession for a TaskItem and return updated aggregates.
    /// </summary>
    [HttpPost("work-sessions")]
    public async Task<ActionResult<WorkSessionResponse>> CreateWorkSessionAsync(
        [FromBody] CreateWorkSessionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Ensure parent task exists
        var task = await _db.Tasks
            .FirstOrDefaultAsync(t => t.Id == request.TaskItemId, ct);

        if (task == null)
            return NotFound($"TaskItem '{request.TaskItemId}' not found.");

        // Create the session
        var session = new WorkSession
        {
            Id = Guid.NewGuid(),
            TaskItemId = task.Id,
            HoursSpent = request.HoursSpent,
            LoggedAt = DateTime.SpecifyKind(request.LoggedAt, DateTimeKind.Utc)
        };

        _db.WorkSessions.Add(session);

        // If you maintain denormalized fields on TaskItem (e.g., ActualHours, UpdatedAt),
        // update them here. Your current model doesn't have them, so we just save.
        await _db.SaveChangesAsync(ct);

        // Return session + updated aggregates
        var totalHours = await _db.WorkSessions
            .Where(ws => ws.TaskItemId == task.Id)
            .SumAsync(ws => (double?)ws.HoursSpent, ct) ?? 0.0;

        var resp = new WorkSessionResponse
        {
            Id = session.Id,
            TaskItemId = task.Id,
            HoursSpent = session.HoursSpent,
            LoggedAt = session.LoggedAt,
            TaskTotalHoursSpent = totalHours,
            TaskEstimatedHours = task.EstimatedHours,
            OverrunStatus = totalHours > task.EstimatedHours ? "OverEstimate" : "OnTrack"
        };

        return CreatedAtAction("GetTaskById", new { id = task.Id }, resp);
    }
}


// this is used to track the productivity of an application
