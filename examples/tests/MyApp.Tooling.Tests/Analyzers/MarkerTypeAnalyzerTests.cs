using Microsoft.CodeAnalysis;
using MyApp.Analyzers;
using MyApp.Tooling.Tests.Infrastructure;

namespace MyApp.Tooling.Tests.Analyzers;

/// <summary>
/// APP3001 — the pattern new analyzers should copy: gate the whole rule once in
/// RegisterCompilationStartAction, resolve the marker once, compare by symbol.
/// The behavioural tests below are the easy half; the one that matters for the
/// talk is <see cref="CompilationWithoutTheMarker_RegistersNothing"/>.
/// </summary>
public sealed class MarkerTypeAnalyzerTests
{
    [Fact]
    public async Task NonSealedEntity_IsReported()
    {
        var source = """
            using MyApp.Demo.Abstractions;

            public class Order : EntityBase;
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new MarkerTypeAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("APP3001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Order", diagnostic.SpanText(source));
        Assert.Contains("Entity 'Order' derives from EntityBase and must be declared 'sealed'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task SealedEntity_IsClean()
    {
        var source = """
            using MyApp.Demo.Abstractions;

            public sealed class Order : EntityBase;
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new MarkerTypeAnalyzer(), source);

        Assert.Empty(diagnostics);
    }

    /// <summary>An abstract entity exists to be derived from — sealing it is impossible.</summary>
    [Fact]
    public async Task AbstractEntity_IsClean()
    {
        var source = """
            using MyApp.Demo.Abstractions;

            public abstract class AuditedEntity : EntityBase;
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new MarkerTypeAnalyzer(), source);

        Assert.Empty(diagnostics);
    }

    /// <summary>The rule walks the whole base chain, not just the immediate base.</summary>
    [Fact]
    public async Task EntityDerivingIndirectly_IsReported()
    {
        var source = """
            using MyApp.Demo.Abstractions;

            public abstract class AuditedEntity : EntityBase;

            public class Order : AuditedEntity;
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new MarkerTypeAnalyzer(), source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Order", diagnostic.SpanText(source));
    }

    [Theory]
    [InlineData("public class Order;")]
    [InlineData("public struct Order;")]
    [InlineData("public interface IOrder;")]
    [InlineData("public class Order : System.Collections.Generic.List<int>;")]
    public async Task TypesOutsideTheMarkerHierarchy_AreClean(string source)
    {
        var diagnostics = await AnalyzerRunner.RunAsync(new MarkerTypeAnalyzer(), source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// The performance point, made as a test: when the marker type is not
    /// referenced, the compilation-start action registers no per-symbol action at
    /// all, so a solution full of unrelated projects does zero work per node. The
    /// observable consequence — no diagnostics — is the same as a scope-check-and-
    /// bail analyzer; the cost is not.
    /// </summary>
    [Fact]
    public async Task CompilationWithoutTheMarker_RegistersNothing()
    {
        var source = """
            namespace Somewhere.Else;

            public abstract class EntityBase;

            public class Order : EntityBase;
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(
            new MarkerTypeAnalyzer(), TestReferences.WithoutAbstractions, source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void SupportedDiagnostics_ExposesOnlyApp3001()
    {
        var rule = Assert.Single(new MarkerTypeAnalyzer().SupportedDiagnostics);

        Assert.Equal("APP3001", rule.Id);
        Assert.Equal("Design", rule.Category);
        Assert.True(rule.IsEnabledByDefault);
        Assert.Equal(DiagnosticSeverity.Warning, rule.DefaultSeverity);
    }
}
