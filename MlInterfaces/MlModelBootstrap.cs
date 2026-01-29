using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace WorkloadProductivity.MlInterfaces
{
    

    public static class MlModelBootstrap
    {
        /// <summary>
        /// Ensures the model file exists at modelPath. If missing, trains from EF history or creates a bootstrap model.
        /// </summary>
        public static void EnsureModelAsync(IServiceProvider services, string modelPath, CancellationToken ct = default)
        {
            var dir = Path.GetDirectoryName(modelPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(modelPath))
                return; // already trained

            // Build training data from EF Core if possible
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;
            const double OverrunThreshold = 1.2; // 20% over estimate => delayed

            // Pull a modest amount of recent tasks for training
            var trainingRows = db.Tasks
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .Take(5000)
                .Select(t => new {
                    t.Id,
                    t.EstimatedHours,
                    t.CreatedAt,
                    Actual = db.WorkSessions.Where(ws => ws.TaskItemId == t.Id).Sum(ws => (double?)ws.HoursSpent) ?? 0.0,
                    Postponements = db.TaskStateHistories.Count(h => h.TaskItemId == t.Id && h.State == Models.TaskState.Postponed),
                    Continuations = db.TaskStateHistories.Count(h => h.TaskItemId == t.Id && h.State == Models.TaskState.Continued)
                })
                .ToList();

            var data = trainingRows.Select(r =>
            {
                var age = (float)(now - r.CreatedAt.ToUniversalTime()).TotalDays;
                var eff = (float)(r.Actual / Math.Max(r.EstimatedHours, 0.0001));
                return new TaskFeaturesWithLabel
                {
                    EstimatedHours = (float)r.EstimatedHours,
                    ActualHours = (float)r.Actual,
                    ContinuationCount = (float)r.Continuations,
                    PostponementCount = (float)r.Postponements,
                    TaskAgeDays = age,
                    EffortOverrunRatio = eff,
                    Label = r.Actual > r.EstimatedHours * OverrunThreshold
                };
            }).ToList();

            // If no historical data yet, create a tiny bootstrap dataset (cold start)
            if (data.Count < 10)
            {
                data = new List<TaskFeaturesWithLabel>
            {
                new() { EstimatedHours=8,  ActualHours=6,  ContinuationCount=0, PostponementCount=0, TaskAgeDays=2, EffortOverrunRatio=0.75f, Label=false },
                new() { EstimatedHours=8,  ActualHours=10, ContinuationCount=1, PostponementCount=0, TaskAgeDays=5, EffortOverrunRatio=1.25f, Label=true  },
                new() { EstimatedHours=4,  ActualHours=3,  ContinuationCount=0, PostponementCount=0, TaskAgeDays=1, EffortOverrunRatio=0.75f, Label=false },
                new() { EstimatedHours=12, ActualHours=16, ContinuationCount=2, PostponementCount=1, TaskAgeDays=7, EffortOverrunRatio=1.33f, Label=true  }
            };
            }

            var ml = new MLContext(seed: 42);

            // Build pipeline
            var features = new[]
            {
            nameof(TaskFeatures.EstimatedHours),
            nameof(TaskFeatures.ActualHours),
            nameof(TaskFeatures.ContinuationCount),
            nameof(TaskFeatures.PostponementCount),
            nameof(TaskFeatures.TaskAgeDays),
            nameof(TaskFeatures.EffortOverrunRatio)
        };

            var trainData = ml.Data.LoadFromEnumerable(data);

            var pipeline =
                ml.Transforms.Concatenate("Features", features)
                  .Append(ml.Transforms.NormalizeMinMax("Features"))
                  .Append(ml.BinaryClassification.Trainers.LbfgsLogisticRegression(
                      labelColumnName: nameof(TaskFeaturesWithLabel.Label),
                      featureColumnName: "Features"));

            var model = pipeline.Fit(trainData);

            // Save model
            ml.Model.Save(model, trainData.Schema, modelPath);
        }
    }
}
