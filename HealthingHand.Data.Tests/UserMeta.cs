namespace HealthingHand.Data.Tests;

public sealed record UserMeta(Type UserClrType, string KeyPropName, string EmailPropName, string DisplayNamePropName, string? PasswordPropName);