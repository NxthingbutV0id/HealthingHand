namespace HealthingHand.Data.Tests.Infrastructure;

public sealed record EntryMeta(Type EntryClrType, string KeyPropName, string? UserFkPropName, string MutablePropName);