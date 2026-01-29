using System;
using WorkloadProductivity.Models;


namespace WorkloadProductivity.Models
{
	public class TaskItem
	{
		public Guid Id { get; set; }

		public string Title { get; set; }

		public double EstimatedHours { get; set; }

		public DateTime CreatedAt { get; set; }

		public Guid UserId { get; set; }

		public User User { get; set; }

		public ICollection<WorkSession> WorkSessions { get; set; } = new List<WorkSession>();
		public ICollection<TaskStateHistory> StateHistory { get; set; } = new List<TaskStateHistory>();
	}
}