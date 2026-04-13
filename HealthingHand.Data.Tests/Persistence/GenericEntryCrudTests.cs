using System.Reflection;
using HealthingHand.Data.Persistence;
using HealthingHand.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

using static HealthingHand.Data.Tests.Infrastructure.EfModelMetadataHelper;
using static HealthingHand.Data.Tests.Infrastructure.ReflectionHelpers;

namespace HealthingHand.Data.Tests.Persistence;

public class GenericEntryCrudTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;
    
    [Fact]
    public void SleepEntry_CRUD_AddGetUpdateDelete_Works() => EntryCrudWorks("Sleep");

    [Fact]
    public void DietEntry_CRUD_AddGetUpdateDelete_Works() => EntryCrudWorks("Diet");

    [Fact]
    public void WorkoutEntry_CRUD_AddGetUpdateDelete_Works() => EntryCrudWorks("Workout");
    
    private void EntryCrudWorks(string kind)
    {
        using var db = new AppDbContext(_fixture.Options);

        var (userMeta, user, userKey) = CreateAndSaveUser(db, kind.ToLowerInvariant());

        var entryMeta = GetEntryMeta(db, kind, userMeta);

        var entry = CreateEntryInstance(db, entryMeta, user, userKey, kind);
        db.Add(entry);
        db.SaveChanges();

        var entryKey = GetPropValue(entry, entryMeta.KeyPropName)
                       ?? throw new InvalidOperationException($"{kind} entry key was null after SaveChanges().");

        var mutableOriginal = GetPropValue(entry, entryMeta.MutablePropName);

        // Read
        db.ChangeTracker.Clear();
        var fetched = db.Find(entryMeta.EntryClrType, entryKey)
                     ?? throw new InvalidOperationException($"Could not find {kind} entry by primary key after insertion.");

        Assert.Equal(entryKey, GetPropValue(fetched, entryMeta.KeyPropName));
        Assert.Equal(mutableOriginal, GetPropValue(fetched, entryMeta.MutablePropName));

        // Update
        var tracked = db.Find(entryMeta.EntryClrType, entryKey)
                     ?? throw new InvalidOperationException($"Could not re-load {kind} entry by key for update.");

        var mutableProp = entryMeta.EntryClrType.GetProperty(entryMeta.MutablePropName, BindingFlags.Instance | BindingFlags.Public)
                         ?? throw new InvalidOperationException($"Mutable property '{entryMeta.MutablePropName}' not found on {kind} entity type.");

        var updatedValue = MakeUpdatedValue(mutableProp.PropertyType, GetPropValue(tracked, entryMeta.MutablePropName));
        SetConvertedPropValue(tracked, entryMeta.MutablePropName, updatedValue);
        db.SaveChanges();

        db.ChangeTracker.Clear();
        var fetchedAfterUpdate = db.Find(entryMeta.EntryClrType, entryKey)
                               ?? throw new InvalidOperationException($"Could not re-load {kind} entry after update.");

        Assert.Equal(updatedValue, GetPropValue(fetchedAfterUpdate, entryMeta.MutablePropName));

        // Delete
        var toRemove = db.Find(entryMeta.EntryClrType, entryKey)
                      ?? throw new InvalidOperationException($"Could not re-load {kind} entry by key for delete.");

        db.Remove(toRemove);
        db.SaveChanges();

        db.ChangeTracker.Clear();
        var deleted = db.Find(entryMeta.EntryClrType, entryKey);
        Assert.Null(deleted);
    }
}