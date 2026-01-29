namespace WorkloadProductivity.MlInterfaces
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

    public class PredictionResult
    {
        public bool IsDelayed { get; set; }
        public float Probability { get; set; }
    }

    public interface ITaskFeatureBuilder
    {
        Task<TaskFeatures> BuildAsync(Guid taskItemId, CancellationToken ct);
    }

    public interface ITaskDelayPredictor
    {
        PredictionResult Predict(TaskFeatures features);
        string MapRisk(float probability);
    }

    public class TaskFeaturesWithLabel : TaskFeatures
    {
        public bool Label { get; set; }
    }
}
