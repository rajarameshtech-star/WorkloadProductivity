using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using WorkloadProductivity.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();
    public DbSet<TaskPrediction> TaskPredictions => Set<TaskPrediction>(); // if used
    public DbSet<TaskStateHistory> TaskStateHistories => Set<TaskStateHistory>(); // if used

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Place the fluent config you already have/that I suggested earlier.
        base.OnModelCreating(modelBuilder);
    }
}
