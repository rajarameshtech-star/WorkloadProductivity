using System;

namespace WorkloadProductivity.Models
{
	public enum TaskState
	{
		New,
		InProgress,
		Postponed,
		Continued,
		Completed
	}
	
	public class TaskStateHistory
	{
		public Guid Id { get; set; }
		public Guid TaskItemId { get; set; }

		public TaskItem TaskItem { get; set; }
		public TaskState State { get; set; } = TaskState.New; // "New" , "InProgress", "Postponed", "Continued", "Completed"

		public DateTime ChangedAt { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}