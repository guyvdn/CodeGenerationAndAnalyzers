using System.Runtime.CompilerServices;

namespace MyApp.Demo.Abstractions;

/// <summary>
/// Hand-written base for every generated enum. Written ONCE, by a human; the
/// generator's only job is to wire each enum up to it (and to emit the
/// converters). Members register themselves from the constructor, so
/// <see cref="All"/> needs nothing generated.
/// </summary>
public abstract class SmartEnum<TEnum>
    where TEnum : class
{
    private static readonly List<TEnum> AllInternal = [];

    // Per-closed-type lock: each enum type guards its own AllInternal, so unrelated
    // enums never contend.
    private static readonly Lock AllLock = new();

    protected SmartEnum()
    {
        lock (AllLock)
        {
            AllInternal.Add(Unsafe.As<TEnum>(this));
        }
    }

    public static IEnumerable<TEnum> All
    {
        get
        {
            // Force full static initialization of TEnum so every member has run this
            // constructor and registered itself. Without it, the first reader can
            // observe a non-empty but INCOMPLETE list — which surfaces as a "no
            // matching element" from the lookups in the generated converters.
            // RunClassConstructor is idempotent, cheap once initialized, and blocks
            // until the static constructor has fully completed.
            RuntimeHelpers.RunClassConstructor(typeof(TEnum).TypeHandle);

            lock (AllLock)
            {
                // Hand back a snapshot, never the live list, so callers never enumerate
                // while a member is still registering.
                return [.. AllInternal];
            }
        }
    }
}

// One base per backing type. The generator picks the right one from the
// EnumType on the attribute — which is why `Value` is typed without a single
// line of hand-written code per enum.
public abstract class IntEnum<TEnum> : SmartEnum<TEnum>
    where TEnum : class
{
    public required int Value { get; init; }
}

public abstract class StringEnum<TEnum> : SmartEnum<TEnum>
    where TEnum : class
{
    public required string Value { get; init; }
}

public abstract class GuidEnum<TEnum> : SmartEnum<TEnum>
    where TEnum : class
{
    public required Guid Value { get; init; }
}

public abstract class ByteEnum<TEnum> : SmartEnum<TEnum>
    where TEnum : class
{
    public required byte Value { get; init; }
}
