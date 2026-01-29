using System.ComponentModel.DataAnnotations;

namespace WorkloadProductivity.DTOs
{
    public class CreateTaskItemRequest
    {
        [Required, StringLength(300)]
        public string Title { get; set; } = string.Empty;

        [Range(0, 100000, ErrorMessage = "Estimated hours must be >= 0.")]
        public double EstimatedHours { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        // Optional: associate to a user now or later
        public Guid? UserId { get; set; }
    }

    public class TaskItemResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public double EstimatedHours { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid UserId { get; set; }

        // Convenience aggregates
        public double TotalHoursSpent { get; set; }
    }

    public class CreateWorkSessionRequest
    {
        [Required]
        public Guid TaskItemId { get; set; }

        [Range(0.01, 100000, ErrorMessage = "HoursSpent must be > 0.")]
        public double HoursSpent { get; set; }

        [Required]
        public DateTime LoggedAt { get; set; }
    }

    public class WorkSessionResponse
    {
        public Guid Id { get; set; }
        public Guid TaskItemId { get; set; }
        public double HoursSpent { get; set; }
        public DateTime LoggedAt { get; set; }

        // Updated aggregates for the parent task
        public double TaskTotalHoursSpent { get; set; }
        public double TaskEstimatedHours { get; set; }
        public string? OverrunStatus { get; set; } // e.g., "OnTrack", "OverEstimate"
    }

}