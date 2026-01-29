using System;
namespace WorkloadProductivity.Models
{
	public class WorkSession
	{
		public Guid Id { get; set; }
		public Guid TaskItemId { get; set; }
		public TaskItem TaskItem { get; set; }
		public double HoursSpent { get; set; }
		public DateTime LoggedAt { get; set; }
	}
}