using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkloadProductivity.Dtos;
using WorkloadProductivity.MlInterfaces;
using WorkloadProductivity.Models;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITaskFeatureBuilder _features;
    private readonly ITaskDelayPredictor _predictor;

    public TasksController(AppDbContext db, ITaskFeatureBuilder features, ITaskDelayPredictor predictor)
    {
        _db = db;
        _features = features;
        _predictor = predictor;
    }

    // -------------------------------
    // Create Task
    // -------------------------------
    [HttpPost]
    public async Task<ActionResult<TaskItemResponse>> CreateTaskAsync(
        [FromBody] CreateTaskItemRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            EstimatedHours = request.EstimatedHours,
            CreatedAt = DateTime.SpecifyKind(request.CreatedAt, DateTimeKind.Utc),
            UserId = request.UserId ?? Guid.Empty
        };

        // Seed initial state = New
        var history = new TaskStateHistory
        {
            Id = Guid.NewGuid(),
            TaskItemId = task.Id,
            State = TaskState.New,
            ChangedAt = task.CreatedAt,
            Reason = "Created"
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.Tasks.Add(task);
        _db.TaskStateHistories.Add(history);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return CreatedAtAction("GetTaskById", new { id = task.Id }, await BuildTaskResponseAsync(task.Id, ct));
    }

    // -------------------------------
    // Get Task (computed aggregates + latest prediction)
    // -------------------------------
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskItemResponse>> GetTaskByIdAsync(Guid id, CancellationToken ct)
    {
        var exists = await _db.Tasks.AsNoTracking().AnyAsync(t => t.Id == id, ct);
        if (!exists) return NotFound();

        var response = await BuildTaskResponseAsync(id, ct);
        return Ok(response);
    }

    // -------------------------------
    // Add Work Session (auto state transition, then inline ML)
    // -------------------------------
    [HttpPost("work-sessions")]
    public async Task<ActionResult<WorkSessionResponse>> CreateWorkSessionAsync(
        [FromBody] CreateWorkSessionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskItemId, ct);
        if (task is null) return NotFound($"TaskItem '{request.TaskItemId}' not found.");

        var latestState = await _db.TaskStateHistories
            .Where(h => h.TaskItemId == task.Id)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => h.State)
            .FirstOrDefaultAsync(ct);

        if (latestState == (TaskState.Completed))
            return Conflict("Cannot add work to a completed task. Reopen by changing state if intended.");

        var session = new WorkSession
        {
            Id = Guid.NewGuid(),
            TaskItemId = task.Id,
            HoursSpent = request.HoursSpent,
            LoggedAt = DateTime.SpecifyKind(request.LoggedAt, DateTimeKind.Utc)
        };

        // If first activity or previously Postponed, move to InProgress
        var createInProgressTransition =
            latestState == (TaskState.New) || latestState == (TaskState.Postponed);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.WorkSessions.Add(session);

        if (createInProgressTransition)
        {
            _db.TaskStateHistories.Add(new TaskStateHistory
            {
                Id = Guid.NewGuid(),
                TaskItemId = task.Id,
                State = (TaskState.InProgress),
                ChangedAt = session.LoggedAt,
                Reason = "Work logged"
            });
        }

        await _db.SaveChangesAsync(ct);

        // Inline ML prediction (based on normalized reads)
        var features = await _features.BuildAsync(task.Id, ct);
        var pred = _predictor.Predict(features);
        var risk = _predictor.MapRisk(pred.Probability);

        _db.TaskPredictions.Add(new TaskPrediction
        {
            Id = Guid.NewGuid(),
            TaskItemId = task.Id,
            DelayProbability = pred.Probability,
            RiskLevel = risk,
            PredictedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Build response aggregates (normalized computes)
        var totalHours = await _db.WorkSessions
            .Where(ws => ws.TaskItemId == task.Id)
            .SumAsync(ws => (double?)ws.HoursSpent, ct) ?? 0.0;

        var currentState = await _db.TaskStateHistories
            .Where(h => h.TaskItemId == task.Id)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => h.State)
            .FirstOrDefaultAsync(ct);

        var resp = new WorkSessionResponse
        {
            Id = session.Id,
            TaskItemId = task.Id,
            HoursSpent = session.HoursSpent,
            LoggedAt = session.LoggedAt,
            CurrentState = currentState,
            TotalHoursSpent = totalHours,
            DelayProbability = pred.Probability,
            RiskLevel = risk
        };

        return CreatedAtAction("GetTaskById", new { id = task.Id }, resp);
    }

    // -------------------------------
    // Change State explicitly
    // -------------------------------
    [HttpPost("state")]
    public async Task<ActionResult<TaskItemResponse>> ChangeStateAsync(
        [FromBody] ChangeTaskStateRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var taskExists = await _db.Tasks.AnyAsync(t => t.Id == request.TaskItemId, ct);
        if (!taskExists) return NotFound($"TaskItem '{request.TaskItemId}' not found.");

        var changedAt = DateTime.SpecifyKind(request.ChangedAt ?? DateTime.UtcNow, DateTimeKind.Utc);

        _db.TaskStateHistories.Add(new TaskStateHistory
        {
            Id = Guid.NewGuid(),
            TaskItemId = request.TaskItemId,
            State = request.NewState,
            ChangedAt = changedAt,
            Reason = request.Reason ?? string.Empty
        });

        await _db.SaveChangesAsync(ct);
        return Ok(await BuildTaskResponseAsync(request.TaskItemId, ct));
    }

    // -------------------------------
    // Mark as Completed (idempotent)
    // -------------------------------
    [HttpPost("complete")]
    public async Task<ActionResult<TaskItemResponse>> CompleteTaskAsync(
        [FromBody] CompleteTaskRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var taskExists = await _db.Tasks.AnyAsync(t => t.Id == request.TaskItemId, ct);
        if (!taskExists) return NotFound($"TaskItem '{request.TaskItemId}' not found.");

        var completedAt = DateTime.SpecifyKind(request.CompletedAt ?? DateTime.UtcNow, DateTimeKind.Utc);

        var latest = await _db.TaskStateHistories
            .Where(h => h.TaskItemId == request.TaskItemId)
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefaultAsync(ct);

        if (latest?.State == (TaskState.Completed))
            return Ok(await BuildTaskResponseAsync(request.TaskItemId, ct)); // already done

        _db.TaskStateHistories.Add(new TaskStateHistory
        {
            Id = Guid.NewGuid(),
            TaskItemId = request.TaskItemId,
            State = TaskState.Completed,
            ChangedAt = completedAt,
            Reason = "Marked complete"
        });

        await _db.SaveChangesAsync(ct);
        return Ok(await BuildTaskResponseAsync(request.TaskItemId, ct));
    }

    // -------------------------------
    // Helper to compute current state & aggregates (normalized)
    // -------------------------------
    private async Task<TaskItemResponse> BuildTaskResponseAsync(Guid taskId, CancellationToken ct)
    {
        var baseInfo = await _db.Tasks
            .AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.EstimatedHours,
                t.CreatedAt,
                t.UserId
            })
            .FirstAsync(ct);

        var totalHoursTask = _db.WorkSessions
            .Where(ws => ws.TaskItemId == taskId)
            .SumAsync(ws => (double?)ws.HoursSpent, ct);

        var latestStateTask = _db.TaskStateHistories
            .Where(h => h.TaskItemId == taskId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new { h.State, h.ChangedAt })
            .FirstOrDefaultAsync(ct);

        var countsTask = _db.TaskStateHistories
            .Where(h => h.TaskItemId == taskId)
            .GroupBy(h => 1)
            .Select(g => new
            {
                Postponements = g.Count(x => x.State == (TaskState.Postponed)),
                Continuations = g.Count(x => x.State == (TaskState.Continued))
            })
            .FirstOrDefaultAsync(ct);

        var latestPredTask = _db.TaskPredictions
            .AsNoTracking()
            .Where(p => p.TaskItemId == taskId)
            .OrderByDescending(p => p.PredictedAt)
            .FirstOrDefaultAsync(ct);

        await Task.WhenAll(totalHoursTask, latestStateTask, countsTask, latestPredTask);

        var totalHours = totalHoursTask.Result ?? 0.0;
        var latestState = latestStateTask.Result;
        var counts = countsTask.Result ?? new { Postponements = 0, Continuations = 0 };
        var latestPred = latestPredTask.Result;

        return new TaskItemResponse
        {
            Id = baseInfo.Id,
            Title = baseInfo.Title,
            EstimatedHours = baseInfo.EstimatedHours,
            CreatedAt = baseInfo.CreatedAt,
            UserId = baseInfo.UserId,
            CurrentState = latestState?.State ?? (TaskState.New),
            LastStateChangedAt = latestState?.ChangedAt,
            TotalHoursSpent = totalHours,
            PostponementCount = counts.Postponements,
            ContinuationCount = counts.Continuations,
            DelayProbability = latestPred?.DelayProbability,
            RiskLevel = latestPred?.RiskLevel
        };
    }
}