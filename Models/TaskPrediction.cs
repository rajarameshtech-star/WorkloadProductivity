using System;

namespace WorkloadProductivity.Models
{

    public class TaskPrediction
    {
        public Guid Id { get; set; }
        public Guid TaskItemId { get; set; }
        public TaskItem TaskItem { get; set; }
        public float DelayProbability { get; set; }
        public string RiskLevel { get; set; } //low/high/medium
        public DateTime PredictedAt { get; set; }
    }
}