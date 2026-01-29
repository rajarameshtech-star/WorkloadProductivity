using System;

namespace WorkloadProductivity.Models
{
	public enum TaskState
	{
		Planned,
		InProgress,
		Continued,
		Postponed,
		Done
	}


	public class TaskStateHistory
	{
		public Guid Id { get; set; }
		public Guid TaskItemId { get; set; }

		public TaskItem TaskItem { get; set; }
		public TaskState State { get; set; }

		public DateTime ChangedAt { get; set; }
	}
}