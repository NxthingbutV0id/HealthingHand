using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthingHand.Data.Tests.Infrastructure;

public sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
}