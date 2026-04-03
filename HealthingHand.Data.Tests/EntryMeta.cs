namespace HealthingHand.Data.Tests;

public sealed record EntryMeta(Type EntryClrType, string KeyPropName, string? UserFkPropName, string MutablePropName);