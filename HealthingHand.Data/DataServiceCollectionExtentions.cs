using HealthingHand.Data.Entries;
using HealthingHand.Data.Persistence;
using HealthingHand.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace HealthingHand.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddHealthingHandData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(connectionString));

        services.AddScoped<IAccountStore, AccountStore>();
        services.AddScoped<ISleepStore, SleepStore>();
        services.AddScoped<IDietStore, DietStore>();
        services.AddScoped<IWorkoutStore, WorkoutStore>();

        services.AddScoped<IDatabase, Database>();
        return services;
    }
}