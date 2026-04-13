using HealthingHand.Data.Persistence;
using HealthingHand.Data.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Tests.Persistence;

public class MigrationTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    /// <summary>
    /// This test ensures that the Migrate() call in the constructor
    /// successfully creates the __EFMigrationsHistory table,
    /// which is essential for tracking applied migrations.
    /// </summary>
    [Fact]
    public void MigrateCreatesEfMigrationHistoryTable()
    {
        using var db = fixture.CreateDb();

        var applied = db.Database.GetAppliedMigrations().ToList();

        Assert.NotEmpty(applied);
    }
    
    /// <summary>
    /// This test is used to check that the database migrations are applied as expected.
    /// </summary>
    [Fact]
    public void Debug_ListAppliedMigrations_And_UserColumns()
    {
        using var db = new AppDbContext(fixture.Options);

        var allMigrations = db.Database.GetMigrations().ToList();
        var appliedMigrations = db.Database.GetAppliedMigrations().ToList();

        Assert.True(allMigrations.Count > 0);

        // This is the key check:
        Assert.Contains(allMigrations, m => m.Contains("RemoveUserWeightKg"));
        Assert.Contains(appliedMigrations, m => m.Contains("RemoveUserWeightKg"));

        using var cmd = fixture.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('Users');";

        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();

        while (reader.Read())
            columns.Add(reader.GetString(1)); // column name

        Assert.DoesNotContain("WeightKg", columns);
    }

    /// <summary>
    /// This test verifies that the DbContext's model contains entity types,
    /// which indicates that the model is properly configured
    /// and can be used for database operations.
    /// </summary>
    [Fact]
    public void DbContextModelContainsEntities()
    {
        using var db = fixture.CreateDb();

        Assert.NotEmpty(db.Model.GetEntityTypes());
    }

    /// <summary>
    /// This test directly queries the SQLite master table
    /// to check for the existence of the __EFMigrationsHistory table,
    /// which is created by Entity Framework Core to track applied migrations.
    /// This ensures that the migration process has successfully set up
    /// the necessary infrastructure for managing database schema changes.
    /// </summary>
    [Fact]
    public void Sqlite_has_migrations_history_table()
    {
        using var cmd = fixture.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        using var reader = cmd.ExecuteReader();

        var tables = new List<string>();
        while (reader.Read())
            tables.Add(reader.GetString(0));

        Assert.Contains("__EFMigrationsHistory", tables);
    }
}