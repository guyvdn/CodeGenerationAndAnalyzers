using Microsoft.CodeAnalysis;
using MyApp.Analyzers;
using MyApp.Tooling.Tests.Infrastructure;

namespace MyApp.Tooling.Tests.Analyzers;

/// <summary>
/// APP2001 — the architecture rule written in the older, string-comparison style.
/// Kept in the demo for contrast with <see cref="MarkerTypeAnalyzerTests"/>, and
/// tested here to the same standard: matching on a display string is slower, not
/// looser, and these tests pin that down.
/// </summary>
public sealed class DbContextAnalyzerTests
{
    private const string EfTypes = """
        using Microsoft.EntityFrameworkCore;

        namespace MyApp.Data;

        public class AppDbContext : DbContext;
        """;

    [Fact]
    public async Task ConstructorTakingADerivedDbContext_IsReported()
    {
        var source = """
            using MyApp.Data;

            public class OrderService
            {
                public OrderService(AppDbContext db) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new DbContextAnalyzer(), EfTypes, source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("APP2001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Constructor of 'OrderService'", diagnostic.GetMessage());
        Assert.Contains("use IDbContextFactory<T> instead", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ConstructorTakingDbContextItself_IsReported()
    {
        var source = """
            using Microsoft.EntityFrameworkCore;

            public class OrderService
            {
                public OrderService(DbContext db) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new DbContextAnalyzer(), source);

        Assert.Single(diagnostics);
    }

    /// <summary>The fix the rule is pushing towards — it must not fire on it.</summary>
    [Fact]
    public async Task ConstructorTakingADbContextFactory_IsClean()
    {
        var source = """
            using Microsoft.EntityFrameworkCore;
            using MyApp.Data;

            public class OrderService
            {
                public OrderService(IDbContextFactory<AppDbContext> factory) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new DbContextAnalyzer(), EfTypes, source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// The rule is about how a service is *constructed*, so an ordinary method
    /// taking a DbContext is deliberately out of scope.
    /// </summary>
    [Fact]
    public async Task MethodTakingADbContext_IsNotAConstructorAndIsClean()
    {
        var source = """
            using MyApp.Data;

            public class OrderService
            {
                public void Run(AppDbContext db) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new DbContextAnalyzer(), EfTypes, source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task EveryOffendingConstructorOverload_IsReported()
    {
        var source = """
            using MyApp.Data;

            public class OrderService
            {
                public OrderService(AppDbContext db) { }

                public OrderService(AppDbContext db, int retries) { }

                public OrderService(int retries) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(new DbContextAnalyzer(), EfTypes, source);

        Assert.Equal(2, diagnostics.Length);
    }

    /// <summary>
    /// The match is on the full metadata name, so a same-named type from somewhere
    /// else is not EF's DbContext and must not trip the rule — the trap a simple
    /// <c>Name == "DbContext"</c> check falls into.
    /// </summary>
    [Fact]
    public async Task AnUnrelatedTypeNamedDbContext_IsClean()
    {
        var source = """
            namespace Legacy.Persistence;

            public class DbContext;

            public class OrderService
            {
                public OrderService(DbContext db) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.RunAsync(
            new DbContextAnalyzer(), TestReferences.WithoutEntityFramework, source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void SupportedDiagnostics_ExposesOnlyApp2001()
    {
        var rule = Assert.Single(new DbContextAnalyzer().SupportedDiagnostics);

        Assert.Equal("APP2001", rule.Id);
        Assert.Equal("Architecture", rule.Category);
        Assert.True(rule.IsEnabledByDefault);
        Assert.Equal(DiagnosticSeverity.Warning, rule.DefaultSeverity);
    }
}
