using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyApp.Demo.Abstractions;

namespace MyApp.Tooling.Tests.Infrastructure;

/// <summary>Which references a test compilation is built with.</summary>
[Flags]
internal enum TestReferences
{
    /// <summary>Everything the test host itself runs against.</summary>
    All = 0,

    /// <summary>Drops EF Core — used to prove APP2001 matches on the full name, not the simple one.</summary>
    WithoutEntityFramework = 1,

    /// <summary>Drops the assembly holding EntityBase — used to prove APP3001's compilation-start gate.</summary>
    WithoutAbstractions = 2,
}

/// <summary>
/// Builds the compilations the tests analyze and generate into.
///
/// References come from the test host's own trusted-platform-assemblies list, so
/// there is nothing to download at test time and the reference set is exactly the
/// .NET version this project targets. The tooling assemblies themselves are held
/// out: a test compilation is a stand-in for a *consumer* project, and referencing
/// MyApp.CodeGen would collide with the GeneratedEnumAttribute the generator
/// injects into it.
/// </summary>
internal static class TestCompilation
{
    private static readonly string[] ExcludedPrefixes =
    [
        "MyApp.CodeGen",
        "MyApp.Analyzers",
        "MyApp.CodeFixes",
        "Microsoft.CodeAnalysis",
        "Microsoft.TestPlatform",
        "Microsoft.VisualStudio.TestPlatform",
        "xunit",
        "testhost",
        "NuGet.",
    ];

    private static readonly string AbstractionsAssembly =
        Path.GetFileNameWithoutExtension(typeof(EntityBase).Assembly.Location);

    private static readonly ImmutableArray<string> PlatformAssemblies = LoadPlatformAssemblies();

    /// <summary>Matches the demo projects: whatever the installed compiler supports.</summary>
    public static CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.Latest);

    public static CSharpCompilation Create(params string[] sources) =>
        Create(TestReferences.All, sources);

    public static CSharpCompilation Create(TestReferences references, params string[] sources) =>
        CSharpCompilation.Create(
            // Unique per compilation: several tests emit and load their result into
            // this process, and two assemblies sharing an identity is asking for trouble.
            assemblyName: $"Tests_{Guid.NewGuid():N}",
            syntaxTrees: sources.Select((source, i) =>
                CSharpSyntaxTree.ParseText(source, ParseOptions, path: $"Test{i}.cs")),
            references: MetadataReferences(references),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

    public static IReadOnlyList<MetadataReference> MetadataReferences(TestReferences references = TestReferences.All)
    {
        var excludeEf = references.HasFlag(TestReferences.WithoutEntityFramework);
        var excludeAbstractions = references.HasFlag(TestReferences.WithoutAbstractions);

        return
        [
            .. PlatformAssemblies
                .Where(path => !(excludeEf && Name(path).StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)))
                .Where(path => !(excludeAbstractions && Name(path).Equals(AbstractionsAssembly, StringComparison.OrdinalIgnoreCase)))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
        ];
    }

    /// <summary>
    /// Fails the test with the full diagnostic text. Compile errors in a generator
    /// test are the whole point of the test, so they must not hide behind an
    /// "expected 0, got 3" message.
    /// </summary>
    public static void AssertCompiles(this Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            $"Expected the compilation to succeed, but it reported {errors.Length} error(s):{Environment.NewLine}" +
            string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
    }

    private static ImmutableArray<string> LoadPlatformAssemblies()
    {
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;

        return
        [
            .. trusted
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Where(path => !ExcludedPrefixes.Any(prefix => Name(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .DistinctBy(Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static string Name(string path) => Path.GetFileNameWithoutExtension(path);
}
