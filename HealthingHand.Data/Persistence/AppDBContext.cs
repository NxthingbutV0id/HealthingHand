using HealthingHand.Data.Entries;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntry> Users => Set<UserEntry>();
    public DbSet<SleepEntry> SleepEntries => Set<SleepEntry>();
    public DbSet<DietEntry> DietEntries => Set<DietEntry>();
    public DbSet<MealItemEntry> MealItems => Set<MealItemEntry>();
    public DbSet<WorkoutEntry> WorkoutEntries => Set<WorkoutEntry>();
    public DbSet<ExerciseEntry> ExerciseEntries => Set<ExerciseEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<SleepEntry>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId);
        
        modelBuilder.Entity<SleepEntry>()
            .HasIndex(s => new { s.UserId, s.SleepDate })
            .IsUnique();
        
        modelBuilder.Entity<UserEntry>(e =>
        {
            e.Property(u => u.Email).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
        });
        
        modelBuilder.Entity<DietEntry>()
            .HasMany(m => m.Items)
            .WithOne(i => i.DietEntry)
            .HasForeignKey(i => i.DietEntryId)
            .OnDelete(DeleteBehavior.Cascade);
        
        
    }
}