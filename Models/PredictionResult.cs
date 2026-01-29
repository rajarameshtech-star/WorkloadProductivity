using System;

namespace WorkloadProductivity.Models
{
	public class PredictionResult
	{
		public bool IsDelayed { get; set; }
		public float Probability { get; set; }
	}
}