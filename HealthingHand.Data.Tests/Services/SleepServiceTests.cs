using System.Security.Claims;
using HealthingHand.Data.Entries;
using HealthingHand.Data.Tests.Infrastructure;
using HealthingHand.Web.Services;
using HealthingHand.Web.Services.SleepItems;
using Microsoft.AspNetCore.Http;

namespace HealthingHand.Data.Tests.Services;

public class SleepServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    [Fact]
    public async Task SaveAsync_Create_AddsNewEntryForCurrentUser()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();
        var service = CreateSleepService(store, userId);

        var input = MakeSleepInput(
            start: new DateTime(2026, 4, 1, 22, 30, 0, DateTimeKind.Utc),
            end:   new DateTime(2026, 4, 2, 6, 45, 0, DateTimeKind.Utc),
            quality: 4.5f,
            notes: "  Slept well  ");

        var result = await service.SaveAsync(input);

        Assert.True(result.Success);
        Assert.Null(result.Error);

        Assert.Single(store.Entries);
        Assert.Equal(1, store.AddCalls);
        Assert.Equal(0, store.UpdateCalls);

        var saved = store.Entries.Single();
        Assert.Equal(userId, saved.UserId);
        Assert.Equal(DateOnly.FromDateTime(input.StartTime), saved.SleepDate);
        Assert.Equal(input.StartTime, saved.StartTime);
        Assert.Equal(input.EndTime, saved.EndTime);
        Assert.Equal("Slept well", saved.Notes);

        // 4.5 * 32 = 144
        Assert.Equal((byte)144, saved.SleepQuality);
    }

    [Fact]
    public async Task SleepService_SaveAsync_Update_SameDateUpdatesExistingEntryInsteadOfAddingAnother()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();

        store.Seed(new SleepEntry
        {
            Id = 7,
            UserId = userId,
            SleepDate = new DateOnly(2026, 4, 1),
            StartTime = new DateTime(2026, 4, 1, 22, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 4, 2, 6, 0, 0, DateTimeKind.Utc),
            SleepQuality = 96, // 3.0
            Notes = "Original"
        });

        var service = CreateSleepService(store, userId);

        var input = MakeSleepInput(
            start: new DateTime(2026, 4, 1, 23, 0, 0, DateTimeKind.Utc),
            end:   new DateTime(2026, 4, 2, 7, 30, 0, DateTimeKind.Utc),
            quality: 4.0f,
            notes: "  Updated note  ");

        var result = await service.SaveAsync(input);

        Assert.True(result.Success);
        Assert.Null(result.Error);

        Assert.Single(store.Entries);
        Assert.Equal(0, store.AddCalls);
        Assert.Equal(1, store.UpdateCalls);

        var saved = store.Entries.Single();
        Assert.Equal(7, saved.Id);
        Assert.Equal(userId, saved.UserId);
        Assert.Equal(new DateOnly(2026, 4, 1), saved.SleepDate);
        Assert.Equal(input.StartTime, saved.StartTime);
        Assert.Equal(input.EndTime, saved.EndTime);
        Assert.Equal("Updated note", saved.Notes);

        // 4.0 * 32 = 128
        Assert.Equal((byte)128, saved.SleepQuality);
    }

    [Fact]
    public async Task ListHistoryAsync_Retrieve_ReturnsOnlyCurrentUsersEntriesInDescendingDateOrder()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        store.Seed(
            new SleepEntry
            {
                Id = 1,
                UserId = userId,
                SleepDate = new DateOnly(2026, 4, 1),
                StartTime = new DateTime(2026, 4, 1, 22, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 4, 2, 6, 0, 0, DateTimeKind.Utc),
                SleepQuality = 128, // 4.0
                Notes = "Older"
            },
            new SleepEntry
            {
                Id = 2,
                UserId = userId,
                SleepDate = new DateOnly(2026, 4, 3),
                StartTime = new DateTime(2026, 4, 3, 23, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 4, 4, 7, 30, 0, DateTimeKind.Utc),
                SleepQuality = 160, // 5.0
                Notes = "Newest"
            },
            new SleepEntry
            {
                Id = 3,
                UserId = otherUserId,
                SleepDate = new DateOnly(2026, 4, 2),
                StartTime = new DateTime(2026, 4, 2, 22, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 4, 3, 6, 0, 0, DateTimeKind.Utc),
                SleepQuality = 64, // 2.0
                Notes = "Other user"
            });

        var service = CreateSleepService(store, userId);

        var history = await service.ListHistoryAsync(
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 5));

        Assert.Equal(2, history.Count);

        Assert.Equal(new DateOnly(2026, 4, 3), history[0].SleepDate);
        Assert.Equal(new DateOnly(2026, 4, 1), history[1].SleepDate);

        Assert.All(history, item => Assert.DoesNotContain("Other user", item.Notes));

        Assert.Equal("Newest", history[0].Notes);
        Assert.Equal("Older", history[1].Notes);
        Assert.True(Math.Abs(history[0].SleepQuality - 5.0f) < 0.001f);
        Assert.True(Math.Abs(history[1].SleepQuality - 4.0f) < 0.001f);
    }

    [Fact]
    public async Task DeleteAsync_Delete_RemovesOwnedEntry()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();

        store.Seed(new SleepEntry
        {
            Id = 10,
            UserId = userId,
            SleepDate = new DateOnly(2026, 4, 1),
            StartTime = new DateTime(2026, 4, 1, 22, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 4, 2, 6, 0, 0, DateTimeKind.Utc),
            SleepQuality = 128,
            Notes = "Delete me"
        });

        var service = CreateSleepService(store, userId);

        var result = await service.DeleteAsync(10);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(1, store.DeleteCalls);
        Assert.Empty(store.Entries);
    }

    [Fact]
    public async Task DeleteAsync_UserIsolation_DoesNotDeleteOtherUsersEntry()
    {
        var store = new FakeSleepStore();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        store.Seed(new SleepEntry
        {
            Id = 25,
            UserId = otherUserId,
            SleepDate = new DateOnly(2026, 4, 1),
            StartTime = new DateTime(2026, 4, 1, 22, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 4, 2, 6, 0, 0, DateTimeKind.Utc),
            SleepQuality = 128,
            Notes = "Other user's entry"
        });

        var service = CreateSleepService(store, userId);

        var result = await service.DeleteAsync(25);

        Assert.False(result.Success);
        Assert.Equal("Sleep entry not found.", result.Error);
        Assert.Equal(0, store.DeleteCalls);
        Assert.Single(store.Entries);
        Assert.Equal(25, store.Entries.Single().Id);
    }
    
    private static SleepService CreateSleepService(FakeSleepStore store, Guid? userId)
    {
        var httpContext = new DefaultHttpContext();

        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                    authenticationType: "TestAuth"));
        }

        return new SleepService(store, new HttpContextAccessor { HttpContext = httpContext });
    }

    private static SleepEntryInput MakeSleepInput(DateTime start, DateTime end, float quality, string notes = "")
    {
        return new SleepEntryInput
        {
            StartTime = start,
            EndTime = end,
            SleepQuality = quality,
            Notes = notes
        };
    }
}