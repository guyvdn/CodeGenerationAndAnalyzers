using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyApp.Tooling.Tests.Infrastructure;

namespace MyApp.Tooling.Tests.CodeGen;

/// <summary>
/// Text assertions prove what the generator wrote; these prove it works. Each test
/// generates, emits and loads the assembly, then drives the generated converters
/// through reflection — the same round trips Program.cs does on stage.
/// </summary>
public sealed class GeneratedConverterTests
{
    private const string IntEnumSource = """
        using MyApp.CodeGen;

        namespace MyApp.Demo;

        [GeneratedEnum(EnumType.Int)]
        public sealed partial class Priority
        {
            public required string Name { get; init; }

            public static Priority Low  { get; } = new() { Value = 1, Name = "Low" };
            public static Priority High { get; } = new() { Value = 2, Name = "High" };

            public override string ToString() => Name;
        }
        """;

    private const string StringEnumSource = """
        using MyApp.CodeGen;

        namespace MyApp.Demo;

        [GeneratedEnum(EnumType.String)]
        public sealed partial class Channel
        {
            public static Channel Email { get; } = new() { Value = "email" };
            public static Channel Sms   { get; } = new() { Value = "sms" };
        }
        """;

    /// <summary>
    /// The generated <c>[JsonConverter]</c> attribute is what makes this work with
    /// no JsonSerializerOptions wiring at the call site.
    /// </summary>
    [Fact]
    public void JsonConverter_WritesTheBareBackingValue()
    {
        var (type, high) = LoadMember(IntEnumSource, "MyApp.Demo.Priority", "High");

        Assert.Equal("2", JsonSerializer.Serialize(high, type));
    }

    /// <summary>
    /// Round-tripping must land on the *same instance*, not an equal copy —
    /// the generated Read() looks the value up in All rather than constructing one.
    /// </summary>
    [Fact]
    public void JsonConverter_RoundTripsToTheSameInstance()
    {
        var (type, high) = LoadMember(IntEnumSource, "MyApp.Demo.Priority", "High");

        var roundTripped = JsonSerializer.Deserialize(JsonSerializer.Serialize(high, type), type);

        Assert.Same(high, roundTripped);
    }

    [Fact]
    public void JsonConverter_ForAStringEnum_ReadsBackCaseInsensitively()
    {
        var (type, email) = LoadMember(StringEnumSource, "MyApp.Demo.Channel", "Email");

        Assert.Equal("\"email\"", JsonSerializer.Serialize(email, type));
        Assert.Same(email, JsonSerializer.Deserialize("\"EMAIL\"", type));
    }

    [Fact]
    public void SqlConverter_StoresTheBackingValueAndRehydratesTheMember()
    {
        var assembly = Generate(IntEnumSource);
        var low = Member(assembly, "MyApp.Demo.Priority", "Low");
        var converter = (ValueConverter)Activator.CreateInstance(
            assembly.GetType("MyApp.Demo.PrioritySqlConverter")!)!;

        var column = converter.ConvertToProvider(low);

        Assert.Equal(1, column);
        Assert.Same(low, converter.ConvertFromProvider(column));
    }

    /// <summary>
    /// The EF converter is typed, not object-based: the column type follows the
    /// EnumType on the attribute, which is what lets EF map the property at all.
    /// </summary>
    [Fact]
    public void SqlConverter_IsTypedToTheBackingType()
    {
        var assembly = Generate(IntEnumSource);
        var converter = (ValueConverter)Activator.CreateInstance(
            assembly.GetType("MyApp.Demo.PrioritySqlConverter")!)!;

        Assert.Equal(assembly.GetType("MyApp.Demo.Priority"), converter.ModelClrType);
        Assert.Equal(typeof(int), converter.ProviderClrType);
    }

    /// <summary>
    /// <c>All</c> is the one thing the generator does NOT emit — it comes from the
    /// hand-written SmartEnum base, where each member registers itself. Generate
    /// the wiring, hand-write the mechanism.
    /// </summary>
    [Fact]
    public void All_ComesFromTheHandWrittenBaseAndSeesEveryMember()
    {
        var assembly = Generate(IntEnumSource);
        var type = assembly.GetType("MyApp.Demo.Priority")!;

        // FlattenHierarchy: All is declared on the base, and reflection does not
        // surface inherited statics without it.
        var all = (System.Collections.IEnumerable)type
            .GetProperty("All", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!
            .GetValue(null)!;

        Assert.Equal(
            ["High", "Low"],
            all.Cast<object>().Select(m => m.ToString()).Order(StringComparer.Ordinal));
    }

    private static Assembly Generate(string source)
    {
        GeneratorRunner.Run(TestCompilation.Create(source), out var output);

        return GeneratorRunner.EmitAndLoad(output);
    }

    private static (Type Type, object Member) LoadMember(string source, string typeName, string memberName)
    {
        var assembly = Generate(source);

        return (assembly.GetType(typeName)!, Member(assembly, typeName, memberName));
    }

    private static object Member(Assembly assembly, string typeName, string memberName) =>
        assembly.GetType(typeName)!
            .GetProperty(memberName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;
}
