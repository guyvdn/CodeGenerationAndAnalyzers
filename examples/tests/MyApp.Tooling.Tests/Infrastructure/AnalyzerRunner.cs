using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MyApp.Tooling.Tests.Infrastructure;

internal static class AnalyzerRunner
{
    /// <summary>
    /// Runs one analyzer over a compilation and returns what it reported.
    ///
    /// An analyzer that throws does not fail the build — Roslyn swallows the
    /// exception and reports AD0001 instead — so a test that only counted APP1001s
    /// would go green on a crashing analyzer. Hence the explicit AD0001 check.
    /// </summary>
    public static async Task<Diagnostic[]> RunAsync(DiagnosticAnalyzer analyzer, CSharpCompilation compilation)
    {
        var diagnostics = await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        var crashes = diagnostics.Where(d => d.Id == "AD0001").ToArray();

        Assert.True(
            crashes.Length == 0,
            "The analyzer threw:" + Environment.NewLine + string.Join(Environment.NewLine, crashes.Select(d => d.ToString())));

        return [.. diagnostics.OrderBy(d => d.Location.SourceSpan.Start)];
    }

    public static Task<Diagnostic[]> RunAsync(DiagnosticAnalyzer analyzer, params string[] sources) =>
        RunAsync(analyzer, TestCompilation.Create(sources));

    public static Task<Diagnostic[]> RunAsync(DiagnosticAnalyzer analyzer, TestReferences references, params string[] sources) =>
        RunAsync(analyzer, TestCompilation.Create(references, sources));

    /// <summary>
    /// The exact source text the squiggle covers. Asserting on this catches a
    /// diagnostic reported at the right line but the wrong span — the kind of bug
    /// a line-number assertion happily accepts.
    /// </summary>
    public static string SpanText(this Diagnostic diagnostic, string source) =>
        source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
}
