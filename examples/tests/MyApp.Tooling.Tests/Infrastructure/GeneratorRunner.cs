using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyApp.CodeGen;

namespace MyApp.Tooling.Tests.Infrastructure;

/// <summary>Drives <see cref="EnumGenerator"/> the way the compiler does.</summary>
internal static class GeneratorRunner
{
    public static GeneratorRunResult Run(CSharpCompilation compilation) =>
        Run(compilation, out _);

    public static GeneratorRunResult Run(CSharpCompilation compilation, out Compilation output)
    {
        var driver = CreateDriver(trackSteps: false)
            .RunGeneratorsAndUpdateCompilation(compilation, out output, out _);

        return driver.GetRunResult().Results.Single();
    }

    /// <summary>
    /// A driver that records why each step ran. Needed by the caching tests — the
    /// incremental pipeline's whole promise is that an unrelated edit re-runs
    /// nothing, and that is only observable through the step reasons.
    /// </summary>
    public static GeneratorDriver CreateDriver(bool trackSteps) =>
        CSharpGeneratorDriver.Create(
            generators: [new EnumGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: TestCompilation.ParseOptions,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: trackSteps));

    public static string Source(this GeneratorRunResult result, string hintName)
    {
        var match = result.GeneratedSources.SingleOrDefault(s => s.HintName == hintName);

        Assert.True(
            match.SourceText is not null,
            $"No generated source named '{hintName}'. Generated: " +
            string.Join(", ", result.GeneratedSources.Select(s => s.HintName)));

        return match.SourceText!.ToString();
    }

    /// <summary>
    /// Emits the post-generation compilation and loads it, so a test can call the
    /// generated converters instead of only reading their text. Text assertions
    /// prove what was written; this proves it works.
    /// </summary>
    public static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            "Emit failed:" + Environment.NewLine +
            string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        return Assembly.Load(stream.ToArray());
    }
}
