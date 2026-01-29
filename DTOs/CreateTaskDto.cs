using System.ComponentModel.DataAnnotations;
using WorkloadProductivity.Models;

namespace WorkloadProductivity.Dtos
{
    
    public class CreateTaskItemRequest
    {
        [Required, StringLength(300)]
        public string Title { get; set; } = string.Empty;

        [Range(0, 100000)]
        public double EstimatedHours { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public Guid? UserId { get; set; }
    }

    public class CreateWorkSessionRequest
    {
        [Required]
        public Guid TaskItemId { get; set; }

        [Range(0.01, 100000)]
        public double HoursSpent { get; set; }

        [Required]
        public DateTime LoggedAt { get; set; }
    }

    public class ChangeTaskStateRequest
    {
        [Required]
        public Guid TaskItemId { get; set; }

        [Required]
        public TaskState NewState { get; set; }

        public string? Reason { get; set; }
        public DateTime? ChangedAt { get; set; }
    }

    public class CompleteTaskRequest
    {
        [Required]
        public Guid TaskItemId { get; set; }

        public DateTime? CompletedAt { get; set; }
    }

    public class TaskItemResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public double EstimatedHours { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid UserId { get; set; }

        // Computed at read time (no denormalization)
        public TaskState CurrentState { get; set; } = TaskState.New;
        public DateTime? LastStateChangedAt { get; set; }
        public double TotalHoursSpent { get; set; }
        public int PostponementCount { get; set; }
        public int ContinuationCount { get; set; }

        // Latest prediction snapshot
        public float? DelayProbability { get; set; }
        public string? RiskLevel { get; set; }
    }

    public class WorkSessionResponse
    {
        public Guid Id { get; set; }
        public Guid TaskItemId { get; set; }
        public double HoursSpent { get; set; }
        public DateTime LoggedAt { get; set; }

        // Aggregates after insert (computed)
        public TaskState CurrentState { get; set; } = TaskState.InProgress;
        public double TotalHoursSpent { get; set; }

        // Inline ML
        public float DelayProbability { get; set; }
        public string RiskLevel { get; set; } = "Low";
    }
}