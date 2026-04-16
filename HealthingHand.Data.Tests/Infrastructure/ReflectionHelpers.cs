using System.Reflection;

namespace HealthingHand.Data.Tests.Infrastructure;

public static class ReflectionHelpers
{
    public static bool IsDefaultValue(object? value, Type type)
    {
        if (value is null) return true;

        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (!t.IsValueType)
        {
            // Reference type (string, etc.)
            return value is string s && string.IsNullOrEmpty(s);
        }

        var defaultValue = Activator.CreateInstance(t);
        return Equals(value, defaultValue);
    }

    public static object MakeDummyValue(Type type, string kind, string propName)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(string)) return $"{kind}_{propName}_{Guid.NewGuid():N}";
        if (t == typeof(byte)) return (byte)10;
        if (t == typeof(int)) return 10;
        if (t == typeof(long)) return 10L;
        if (t == typeof(short)) return (short)10;
        if (t == typeof(double)) return 10.5;
        if (t == typeof(float)) return 10.5f;
        if (t == typeof(decimal)) return 10.5m;
        if (t == typeof(bool)) return true;
        if (t == typeof(DateTime)) return DateTime.UtcNow;
        if (t == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow;
        if (t == typeof(TimeSpan)) return TimeSpan.FromMinutes(30);
        if (t == typeof(Guid)) return Guid.NewGuid();

        switch (t.FullName)
        {
            // .NET 6+ types
            case "System.DateOnly":
            {
                var dt = DateTime.UtcNow;
                var dateOnlyFrom = t.GetMethod("FromDateTime", BindingFlags.Public | BindingFlags.Static);
                return dateOnlyFrom?.Invoke(null, [dt])
                       ?? throw new InvalidOperationException("Could not construct DateOnly value.");
            }
            case "System.TimeOnly":
            {
                var dt = DateTime.UtcNow;
                var timeOnlyFrom = t.GetMethod("FromDateTime", BindingFlags.Public | BindingFlags.Static);
                return timeOnlyFrom?.Invoke(null, [dt])
                       ?? throw new InvalidOperationException("Could not construct TimeOnly value.");
            }
        }

        if (t.IsEnum)
        {
            var values = Enum.GetValues(t);
            return values.Length > 0
                ? values.GetValue(0)!
                : Activator.CreateInstance(t)!;
        }

        throw new InvalidOperationException($"No dummy value generator for required property type '{t.FullName}'. Property: {propName}.");
    }

    public static object MakeInitialMutableValue(Type type, string kind)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return $"{kind}_original";
        if (t == typeof(int)) return 10;
        if (t == typeof(long)) return 10L;
        if (t == typeof(short)) return (short)10;
        if (t == typeof(double)) return 10.0;
        if (t == typeof(float)) return 10.0f;
        if (t == typeof(decimal)) return 10m;
        if (t == typeof(bool)) return true;
        if (t == typeof(DateTime)) return DateTime.UtcNow;
        if (t == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow;
        if (t == typeof(TimeSpan)) return TimeSpan.FromMinutes(30);
        return t == typeof(Guid) ? Guid.NewGuid() :
            // DateOnly/TimeOnly/enums fallback
            MakeDummyValue(type, kind, "Mutable");
    }

    public static object MakeUpdatedValue(Type type, object? current)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        // I wish this could be a switch expression, this is so ugly... - CT
        if (t == typeof(string)) return $"updated_{Guid.NewGuid():N}";
        if (t == typeof(int)) return current is int i ? i + 1 : 11;
        if (t == typeof(long)) return current is long l ? l + 1 : 11L;
        if (t == typeof(short)) return (short)(current is short s ? s + 1 : 11);
        if (t == typeof(double)) return current is double d ? d + 1.0 : 11.0;
        if (t == typeof(float)) return current is float f ? f + 1.0f : 11.0f;
        if (t == typeof(decimal)) return current is decimal m ? m + 1m : 11m;
        if (t == typeof(bool)) return current is false;
        if (t == typeof(DateTime)) return current is DateTime dt ? dt.AddMinutes(5) : DateTime.UtcNow.AddMinutes(5);
        if (t == typeof(DateTimeOffset)) return current is DateTimeOffset dto ? dto.AddMinutes(5) : DateTimeOffset.UtcNow.AddMinutes(5);
        if (t == typeof(TimeSpan)) return current is TimeSpan ts ? ts + TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(35);
        if (t == typeof(Guid)) return Guid.NewGuid();

        if (!t.IsEnum) return MakeDummyValue(type, "updated", "Mutable");
        var values = Enum.GetValues(t);
        return values.Length switch
        {
            >= 2 => values.GetValue(1)!,
            1 => values.GetValue(0)!,
            // DateOnly/TimeOnly fallback: just generate a new value.
            _ => MakeDummyValue(type, "updated", "Mutable")
        };
    }

    public static string? PickFirstExistingProperty(Type t, params string[] candidates)
    {
        var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Select(p => p.Name)
                     .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (from c in candidates
            where props.Contains(c)
            select t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .First(p => string.Equals(p.Name, c, StringComparison.OrdinalIgnoreCase))
                .Name).FirstOrDefault();
    }

    public static object? GetPropValue(object obj, string propName)
    {
        var prop = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
        return prop?.GetValue(obj);
    }

    public static void SetPropValue(object obj, string propName, object value)
    {
        var prop = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
        if (prop is null) throw new InvalidOperationException($"Property '{propName}' not found on type '{obj.GetType().Name}'.");

        prop.SetValue(obj, value);
    }

    public static void SetConvertedPropValue(object obj, string propName, object value)
    {
        var prop = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
        if (prop is null) throw new InvalidOperationException($"Property '{propName}' not found on type '{obj.GetType().Name}'.");

        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        var converted = value;
        if (!targetType.IsInstanceOfType(value))
        {
            if (targetType == typeof(Guid) && value is string s)
            {
                converted = Guid.Parse(s);
            }
            else if (targetType == typeof(string) && value is Guid g)
            {
                converted = g.ToString();
            }
            else if (targetType.IsEnum)
            {
                converted = value is string es
                    ? Enum.Parse(targetType, es)
                    : Enum.ToObject(targetType, value);
            }
            else
            {
                converted = Convert.ChangeType(value, targetType);
            }
        }

        prop.SetValue(obj, converted);
    }
}