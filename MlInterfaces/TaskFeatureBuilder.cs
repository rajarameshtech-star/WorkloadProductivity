using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ML;
using Microsoft.ML;
using WorkloadProductivity.Dtos;
using WorkloadProductivity.Models;

namespace WorkloadProductivity.MlInterfaces
{
    public class TaskFeatureBuilder : ITaskFeatureBuilder
    {
        private readonly AppDbContext _db;
        public TaskFeatureBuilder(AppDbContext db) => _db = db;

        public async Task<TaskFeatures> BuildAsync(Guid taskItemId, CancellationToken ct)
        {
            var task = await _db.Tasks
                .AsNoTracking()
                .Where(t => t.Id == taskItemId)
                .Select(t => new
                {
                    t.Id,
                    t.EstimatedHours,
                    t.CreatedAt,
                    Actual = _db.WorkSessions
                        .Where(ws => ws.TaskItemId == t.Id)
                        .Sum(ws => (double?)ws.HoursSpent) ?? 0.0,
                    Postponements = _db.TaskStateHistories
                        .Count(h => h.TaskItemId == t.Id && h.State == (TaskState.Postponed)),
                    Continuations = _db.TaskStateHistories
                        .Count(h => h.TaskItemId == t.Id && h.State == (TaskState.Continued))
                })
                .FirstOrDefaultAsync(ct);

            if (task is null) throw new KeyNotFoundException($"Task {taskItemId} not found.");

            var now = DateTime.UtcNow;
            var age = (float)(now - task.CreatedAt.ToUniversalTime()).TotalDays;

            return new TaskFeatures
            {
                EstimatedHours = (float)task.EstimatedHours,
                ActualHours = (float)task.Actual,
                PostponementCount = task.Postponements,
                ContinuationCount = task.Continuations,
                TaskAgeDays = age,
                EffortOverrunRatio = (float)(task.Actual / Math.Max(task.EstimatedHours, 0.0001))
            };
        }
    }


        public class TaskDelayPredictor : ITaskDelayPredictor
        {
            private readonly PredictionEngine<TaskFeatures, PredictionResult> _engine;
            public TaskDelayPredictor(ITransformer model, MLContext ml)
                => _engine = ml.Model.CreatePredictionEngine<TaskFeatures, PredictionResult>(model);

            public PredictionResult Predict(TaskFeatures features) => _engine.Predict(features);

            public string MapRisk(float p) => p < 0.33f ? "Low" : p < 0.66f ? "Medium" : "High";
    }


            public class PooledTaskDelayPredictor : ITaskDelayPredictor
            {
                private readonly PredictionEnginePool<TaskFeatures, PredictionResult> _pool;
                public PooledTaskDelayPredictor(PredictionEnginePool<TaskFeatures, PredictionResult> pool)
                    => _pool = pool;

                public PredictionResult Predict(TaskFeatures f)
                    => _pool.Predict(modelName: "DelayModel", example: f);

                public string MapRisk(float p) => p < 0.33f ? "Low" : p < 0.66f ? "Medium" : "High";
            }
}
