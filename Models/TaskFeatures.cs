using System;

namespace WorkloadProductivity.Models
{

	public class TaskFeatures
	{
		public float EstimatedHours { get; set; }
		public float ActualHours { get; set; }
		public float ContinuationCount { get; set; }
		public float PostponementCount { get; set; }
		public float TaskAgeDays { get; set; }
		public float EffortOverrunRatio { get; set; }
	}
}