using HealthingHand.Data.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Tests.Infrastructure;

public class SqliteTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbContextOptions<AppDbContext> Options { get; }
    public IDbContextFactory<AppDbContext> Factory { get; }

    public SqliteTestFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(Options);
        db.Database.Migrate();

        Factory = new TestDbContextFactory(Options);
    }

    public AppDbContext CreateDb() => new(Options);

    public void Dispose()
    {
        _connection.Dispose();
    }
    
    public SqliteCommand CreateCommand() => _connection.CreateCommand();
}