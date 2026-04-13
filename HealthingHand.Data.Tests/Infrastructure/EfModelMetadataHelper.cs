using System.Reflection;
using HealthingHand.Data.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata;

using static HealthingHand.Data.Tests.Infrastructure.ReflectionHelpers;

namespace HealthingHand.Data.Tests.Infrastructure;

public static class EfModelMetadataHelper
{
    public static UserMeta GetUserMeta(AppDbContext db)
    {
        var entityTypes = db.Model.GetEntityTypes().ToList();

        var userEntity = entityTypes
            .FirstOrDefault(et =>
            {
                var clr = et.ClrType;
                var props = clr.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var hasEmail = props.Contains("Email") || props.Contains("EmailAddress");
                var hasKey = et.FindPrimaryKey() != null;
                return hasEmail && hasKey;
            });

        if (userEntity is null)
        {
            throw new InvalidOperationException(
                "Could not locate a User entity in the EF model. Ensure you have a mapped entity with an Email/EmailAddress property.");
        }

        var key = userEntity.FindPrimaryKey()!;
        if (key.Properties.Count != 1)
            throw new InvalidOperationException($"User entity primary key must be a single-column key for these tests. Found {key.Properties.Count} columns.");

        var keyPropName = key.Properties[0].Name;

        var emailPropName = PickFirstExistingProperty(userEntity.ClrType, "Email", "EmailAddress")
                            ?? throw new InvalidOperationException("User entity does not contain an Email or EmailAddress property.");

        var displayNamePropName = PickFirstExistingProperty(userEntity.ClrType, "DisplayName", "Name", "Username")
                                  ?? throw new InvalidOperationException("User entity does not contain DisplayName/Name/Username property. Add one or update the test.");

        var passwordPropName = PickFirstExistingProperty(
            userEntity.ClrType,
            "PasswordHash",
            "HashedPassword",
            "Password");

        return new UserMeta(userEntity.ClrType, keyPropName, emailPropName, displayNamePropName, passwordPropName);
    }

    public static EntryMeta GetEntryMeta(AppDbContext db, string kind, UserMeta userMeta)
    {
        var entityTypes = db.Model.GetEntityTypes().ToList();
        var userEntityType = db.Model.FindEntityType(userMeta.UserClrType)
                            ?? throw new InvalidOperationException("Could not locate User entity type in EF model.");

        var candidates = entityTypes
            .Where(et => et.ClrType.Name.Contains(kind, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"Could not locate an entity type whose CLR name contains '{kind}'. Ensure you have a mapped '{kind}' entry entity (e.g., {kind}Entry).");
        }

        IEntityType? best = null;
        var bestScore = int.MinValue;

        foreach (var et in candidates)
        {
            if (et.FindPrimaryKey() is null) continue;

            var score = 0;
            var name = et.ClrType.Name;
            if (name.Equals(kind, StringComparison.OrdinalIgnoreCase)) score += 4;
            if (name.EndsWith(kind, StringComparison.OrdinalIgnoreCase)) score += 2;
            if (name.EndsWith($"{kind}Entry", StringComparison.OrdinalIgnoreCase)) score += 3;

            var hasUserFk = et.GetForeignKeys().Any(fk => fk.PrincipalEntityType == userEntityType && fk.Properties.Count == 1);
            if (hasUserFk) score += 5;

            var hasEditableScalar = et.GetProperties().Any(p =>
                p.PropertyInfo is not null && p.PropertyInfo.CanWrite &&
                p.ValueGenerated == ValueGenerated.Never &&
                (p.ClrType == typeof(string) || p.ClrType == typeof(int) || p.ClrType == typeof(long) || p.ClrType == typeof(double) ||
                 p.ClrType == typeof(float) || p.ClrType == typeof(decimal) || p.ClrType == typeof(DateTime) || p.ClrType == typeof(TimeSpan) ||
                 p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(Guid)));

            if (hasEditableScalar) score += 2;

            if (score <= bestScore) continue;
            bestScore = score;
            best = et;
        }

        var entity = best ?? candidates[0];

        var pk = entity.FindPrimaryKey()!;
        if (pk.Properties.Count != 1)
            throw new InvalidOperationException($"{kind} entry primary key must be a single-column key for these tests. Found {pk.Properties.Count} columns.");

        var keyPropName = pk.Properties[0].Name;

        // FK to User (best-effort)
        var userFk = entity.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType == userEntityType && fk.Properties.Count == 1);

        var userFkPropName = userFk?.Properties[0].Name;

        // Pick a mutable scalar property to validate + update
        var mutablePropName = PickMutableProperty(entity, keyPropName, userFkPropName)
                              ?? throw new InvalidOperationException(
                                  $"Could not locate a mutable scalar property on '{entity.ClrType.Name}' to use for an update test. " +
                                  "Add a writable scalar property (e.g., Notes/Duration/Calories) or update PickMutableProperty().");

        return new EntryMeta(entity.ClrType, keyPropName, userFkPropName, mutablePropName);
    }

    public static string? PickMutableProperty(IEntityType entity, string keyPropName, string? userFkPropName)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyPropName };
        if (!string.IsNullOrWhiteSpace(userFkPropName)) excluded.Add(userFkPropName);

        // Prefer common "editable" fields first
        var preferredNames = new[]
        {
            "Notes", "Comment", "Description", "Details",
            "Quality", "Duration", "Hours", "Minutes",
            "Calories", "CaloriesBurned", "Protein", "Carbs", "Fat",
            "Steps", "Distance", "Reps", "Sets", "Weight",
            "Name", "Title"
        };

        foreach (var pref in preferredNames)
        {
            var p = entity.GetProperties().FirstOrDefault(x =>
                !excluded.Contains(x.Name) &&
                x.PropertyInfo is not null && x.PropertyInfo.CanWrite &&
                x.ValueGenerated == ValueGenerated.Never &&
                string.Equals(x.Name, pref, StringComparison.OrdinalIgnoreCase));

            if (p is not null) return p.Name;
        }

        // Fallback: first reasonable scalar
        var fallback = entity.GetProperties().FirstOrDefault(p =>
            !excluded.Contains(p.Name) &&
            p.PropertyInfo is not null && p.PropertyInfo.CanWrite &&
            p.ValueGenerated == ValueGenerated.Never &&
            (p.ClrType == typeof(string) || p.ClrType == typeof(int) || p.ClrType == typeof(long) || p.ClrType == typeof(double) ||
             p.ClrType == typeof(float) || p.ClrType == typeof(decimal) || p.ClrType == typeof(DateTime) || p.ClrType == typeof(TimeSpan) ||
             p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(Guid)));

        return fallback?.Name;
    }

    public static (UserMeta meta, object user, object userKey) CreateAndSaveUser(AppDbContext db, string prefix)
    {
        var userMeta = GetUserMeta(db);
        var email = $"{prefix}_{Guid.NewGuid():N}@example.com";
        var display = $"{prefix} user";

        var user = CreateUserInstance(userMeta, email, display);
        db.Add(user);
        db.SaveChanges();

        var userKey = GetPropValue(user, userMeta.KeyPropName)
                      ?? throw new InvalidOperationException("User key was null after SaveChanges().");

        return (userMeta, user, userKey);
    }

    public static object CreateUserInstance(UserMeta meta, string email, string displayName)
    {
        var user = Activator.CreateInstance(meta.UserClrType)
                   ?? throw new InvalidOperationException($"Could not create instance of {meta.UserClrType.FullName} (missing public parameterless constructor?).");

        var keyProp = meta.UserClrType.GetProperty(meta.KeyPropName, BindingFlags.Instance | BindingFlags.Public);
        if (keyProp is not null && keyProp.PropertyType == typeof(Guid))
        {
            var current = (Guid)(keyProp.GetValue(user) ?? Guid.Empty);
            if (current == Guid.Empty)
                keyProp.SetValue(user, Guid.NewGuid());
        }

        SetPropValue(user, meta.EmailPropName, email);
        SetPropValue(user, meta.DisplayNamePropName, displayName);

        // Set a default password if the entity has a password property.
        SetUserPassword(user, meta, "DefaultTestPassword123!");

        return user;
    }

    public static object CreateEntryInstance(AppDbContext db, EntryMeta meta, object userInstance, object userKey, string kind)
    {
        var entry = Activator.CreateInstance(meta.EntryClrType)
                    ?? throw new InvalidOperationException($"Could not create instance of {meta.EntryClrType.FullName} (missing public parameterless constructor?).");

        var entityType = db.Model.FindEntityType(meta.EntryClrType)
                        ?? throw new InvalidOperationException($"Could not locate EF entity type for CLR type {meta.EntryClrType.FullName}.");

        // Ensure Guid PK is set if necessary (helps Find()).
        var keyProp = meta.EntryClrType.GetProperty(meta.KeyPropName, BindingFlags.Instance | BindingFlags.Public);
        if (keyProp is not null && keyProp.PropertyType == typeof(Guid))
        {
            var current = (Guid)(keyProp.GetValue(entry) ?? Guid.Empty);
            if (current == Guid.Empty)
                keyProp.SetValue(entry, Guid.NewGuid());
        }

        // Set FK to User if we can find it.
        if (!string.IsNullOrWhiteSpace(meta.UserFkPropName))
        {
            SetConvertedPropValue(entry, meta.UserFkPropName!, userKey);
        }
        else
        {
            // Fall back to navigation property if a scalar FK wasn't discovered.
            var nav = meta.EntryClrType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => p.CanWrite && p.PropertyType.IsInstanceOfType(userInstance));

            nav?.SetValue(entry, userInstance);
        }

        // Set required scalar properties to non-default values.
        foreach (var p in entityType.GetProperties())
        {
            if (p.IsPrimaryKey()) continue;
            if (p.ValueGenerated != ValueGenerated.Never) continue; // store-generated
            if (p.PropertyInfo is null || !p.PropertyInfo.CanWrite) continue;

            // Skip FK we already set.
            if (!string.IsNullOrWhiteSpace(meta.UserFkPropName) &&
                string.Equals(p.Name, meta.UserFkPropName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // If it's required (NOT NULL), ensure it's set to something other than default.
            if (p.IsNullable) continue;
            
            var current = GetPropValue(entry, p.Name);
            if (!IsDefaultValue(current, p.ClrType)) continue;
            
            var v = MakeDummyValue(p.ClrType, kind, p.Name);
            SetConvertedPropValue(entry, p.Name, v);
        }

        // Ensure the mutable property has a predictable initial value for the update test.
        var mutableProp = meta.EntryClrType.GetProperty(meta.MutablePropName, BindingFlags.Instance | BindingFlags.Public)
                         ?? throw new InvalidOperationException($"Mutable property '{meta.MutablePropName}' not found on {meta.EntryClrType.Name}.");

        var initialMutable = MakeInitialMutableValue(mutableProp.PropertyType, kind);
        SetConvertedPropValue(entry, meta.MutablePropName, initialMutable);

        return entry;
    }
    
    public static void SetUserPassword(object user, UserMeta meta, string rawPassword)
    {
        if (string.IsNullOrWhiteSpace(meta.PasswordPropName)) return;

        var prop = user.GetType().GetProperty(meta.PasswordPropName!, BindingFlags.Instance | BindingFlags.Public)
                   ?? throw new InvalidOperationException(
                       $"Password property '{meta.PasswordPropName}' not found on type '{user.GetType().Name}'.");

        string storedValue;

        // If the property name suggests a hash, hash it.
        if (prop.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase))
        {
            var hasher = new PasswordHasher<object>();
            storedValue = hasher.HashPassword(user, rawPassword);
        }
        else
        {
            storedValue = rawPassword;
        }

        prop.SetValue(user, storedValue);
    }
}