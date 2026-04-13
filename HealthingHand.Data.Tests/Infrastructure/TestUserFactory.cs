using HealthingHand.Data.Entries;

namespace HealthingHand.Data.Tests.Infrastructure;

public class TestUserFactory
{
    public static UserEntry MakeUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = "Test User",
        PasswordHash = "testhash",
        CreationDate = DateTime.UtcNow,
        LastOnline = DateTime.UtcNow,
        Age = 20,
        Sex = Sex.Undefined,
        HeightM = 1.75f
    };
}