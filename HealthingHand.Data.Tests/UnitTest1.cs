using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Tests;

public sealed class UnitTest1 : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public UnitTest1()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.Migrate();
    }
    
    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void MigrateCreatesEfMigrationHistoryTable()
    {
        using var db = new AppDbContext(_options);
        
        var applied = db.Database.GetAppliedMigrations().ToList();
        
        Assert.NotEmpty(applied);
    }

    [Fact]
    public void DbContextModelContainsEntities()
    {
        using var db = new AppDbContext(_options);
        
        Assert.NotEmpty(db.Model.GetEntityTypes());
    }
    
    [Fact]
    public void Sqlite_has_migrations_history_table()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        using var reader = cmd.ExecuteReader();

        var tables = new List<string>();
        while (reader.Read())
            tables.Add(reader.GetString(0));

        Assert.Contains("__EFMigrationsHistory", tables);
    }
}
