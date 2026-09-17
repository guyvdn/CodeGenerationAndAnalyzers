using MyApp.CodeGen;

namespace MyApp.Demo;

// The ONLY code a developer writes: intent + values.
// EnumGenerator emits the base list — `: IntEnum<Priority>`, because EnumType.Int
// — into a matching `partial class Priority`. That base is where `Value` (typed)
// and `All` come from; members register themselves in the SmartEnum constructor.
// Remove the generator and this class no longer compiles: no base, no `Value`.
// Generation is load-bearing here.
//
// Flip EnumType.Int -> EnumType.String and the base becomes StringEnum<Priority>,
// `Value` becomes a string, and both converters change with it — no other edits.
// One source of truth, zero duplicated boilerplate to keep in sync.
[GeneratedEnum(EnumType.Int)]
public sealed partial class Priority
{
    public required string Name { get; init; }

    public static Priority Low  { get; } = new() { Value = 1, Name = "Low" };
    public static Priority High { get; } = new() { Value = 2, Name = "High" };

    public override string ToString() => Name;
}
