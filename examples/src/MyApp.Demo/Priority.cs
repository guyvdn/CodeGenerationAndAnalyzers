using MyApp.CodeGen;

namespace MyApp.Demo;

// The ONLY code a developer writes: intent + values.
// EnumGenerator emits the typed `Value` property (int, because EnumType.Int)
// into a matching `partial class Priority`. Remove the generator and this class
// no longer compiles — `Value` is undefined. Generation is load-bearing here.
//
// Flip EnumType.Int -> EnumType.String and `Value` becomes a string, with no
// other edits: one source of truth, zero duplicated boilerplate to keep in sync.
[GeneratedEnum(EnumType.Int)]
public sealed partial class Priority
{
    public required string Name { get; init; }

    public static Priority Low  { get; } = new() { Value = 1, Name = "Low" };
    public static Priority High { get; } = new() { Value = 2, Name = "High" };

    public override string ToString() => Name;
}
