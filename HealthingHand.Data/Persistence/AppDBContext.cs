using HealthingHand.Data.Entries;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntry> Users => Set<UserEntry>();
    public DbSet<SleepEntry> SleepEntries => Set<SleepEntry>();
    public DbSet<SleepGoalEntry> SleepGoals => Set<SleepGoalEntry>();
    public DbSet<DietEntry> DietEntries => Set<DietEntry>();
    public DbSet<MealItemEntry> MealItems => Set<MealItemEntry>();
    public DbSet<WorkoutEntry> WorkoutEntries => Set<WorkoutEntry>();
    public DbSet<ExerciseEntry> ExerciseEntries => Set<ExerciseEntry>();
    public DbSet<WeightEntry> WeightEntries => Set<WeightEntry>();
    public DbSet<WeightGoalEntry> WeightGoals => Set<WeightGoalEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<UserEntry>(e =>
        {
            e.Property(u => u.Email).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
        });
        
        modelBuilder.Entity<UserEntry>()
            .Property(u => u.Sex)
            .HasConversion<string>();
        
        modelBuilder.Entity<SleepEntry>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId);
        
        modelBuilder.Entity<SleepEntry>()
            .HasIndex(s => new { s.UserId, s.SleepDate })
            .IsUnique();

        modelBuilder.Entity<SleepGoalEntry>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId);

        modelBuilder.Entity<SleepGoalEntry>()
            .HasIndex(s => s.UserId)
            .IsUnique();

        modelBuilder.Entity<SleepGoalEntry>(e =>
        {
            e.Property(s => s.PreferredSleepHours).IsRequired();
            e.Property(s => s.CreatedAt).IsRequired();
            e.Property(s => s.UpdatedAt).IsRequired();
        });
        
        modelBuilder.Entity<DietEntry>()
            .HasMany(m => m.Items)
            .WithOne(i => i.DietEntry)
            .HasForeignKey(i => i.DietEntryId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<WorkoutEntry>()
            .HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId);
        
        modelBuilder.Entity<WorkoutEntry>()
            .Property(w => w.WorkoutType)
            .HasConversion<string>();

        modelBuilder.Entity<WorkoutEntry>()
            .HasMany(w => w.Exercises)
            .WithOne(e => e.WorkoutEntry)
            .HasForeignKey(e => e.WorkoutId)
            .OnDelete(DeleteBehavior.Cascade);

        // Helpful index for “my workouts ordered by date”
        modelBuilder.Entity<WorkoutEntry>()
            .HasIndex(w => new { w.UserId, w.StartedAt });

        // Optional “required” fields
        modelBuilder.Entity<WorkoutEntry>(e =>
        {
            e.Property(w => w.WorkoutType).IsRequired();
            e.Property(w => w.StartedAt).IsRequired();
            e.Property(w => w.SelfReportedIntensity).IsRequired();
            e.Property(w => w.AverageHeartRate).IsRequired();
        });

        modelBuilder.Entity<ExerciseEntry>(e =>
        {
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.ActivityType).HasConversion<string>().IsRequired();
        });
        
        modelBuilder.Entity<ExerciseEntry>()
            .Property(e => e.Time)
            .HasConversion(
                v => (long)v.TotalSeconds,
                v => TimeSpan.FromSeconds(v)
            );
        
        modelBuilder.Entity<WeightEntry>()
            .HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId);

        modelBuilder.Entity<WeightEntry>()
            .HasIndex(w => new { w.UserId, w.Date })
            .IsUnique();

        modelBuilder.Entity<WeightGoalEntry>()
            .HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId);

        modelBuilder.Entity<WeightGoalEntry>()
            .HasIndex(w => w.UserId)
            .IsUnique();

        modelBuilder.Entity<WeightGoalEntry>()
            .Property(w => w.GoalType)
            .HasConversion<string>();

        modelBuilder.Entity<WeightGoalEntry>()
            .Property(w => w.PacePreference)
            .HasConversion<string>();

        modelBuilder.Entity<WeightGoalEntry>(e =>
        {
            e.Property(w => w.CurrentWeightKg).IsRequired();
            e.Property(w => w.GoalWeightKg).IsRequired();
            e.Property(w => w.ExerciseFrequency).IsRequired();
            e.Property(w => w.ExerciseIntensity).IsRequired();
            e.Property(w => w.CreatedAt).IsRequired();
            e.Property(w => w.UpdatedAt).IsRequired();
        });
    }
}
