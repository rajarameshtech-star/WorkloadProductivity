using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ML;
using Microsoft.ML;
using WorkloadProductivity.MlInterfaces;
internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("InitialConnection")));
        // Add services to the container.

        // Load ML model once; inject builder and predictor

        var tempProvider = builder.Services.BuildServiceProvider(); // temp for startup
        var modelPath = Path.Combine(builder.Environment.ContentRootPath, "Models", "task_delay_model.zip");
        MlModelBootstrap.EnsureModelAsync(tempProvider, modelPath);
        if (tempProvider is IDisposable d) d.Dispose(); // dispose temp provider

        // 3) Register PredictionEnginePool from file (thread-safe)
        builder.Services
            .AddPredictionEnginePool<TaskFeatures, PredictionResult>()
            .FromFile(
                modelName: "DelayModel",
                filePath: modelPath,
                watchForChanges: true);

        // 4) Register your feature builder & predictor that uses the pool
        builder.Services.AddScoped<ITaskFeatureBuilder, TaskFeatureBuilder>();
        builder.Services.AddScoped<ITaskDelayPredictor, PooledTaskDelayPredictor>();


        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}