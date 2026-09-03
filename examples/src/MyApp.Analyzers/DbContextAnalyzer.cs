using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MyApp.Analyzers;

/// <summary>
/// APP2001 — a constructor taking a <c>DbContext</c> should take an
/// <c>IDbContextFactory&lt;T&gt;</c> instead. A team convention turned into a
/// build warning. (Only fires when EF Core is referenced by the target project.)
///
/// NOTE: this is the OLDER, string-based style — it compares display strings.
/// Fine for its scope, but new analyzers should prefer the marker-symbol pattern
/// in <see cref="MarkerTypeAnalyzer"/> (resolve once, compare by symbol) to avoid
/// allocating a string per node.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DbContextAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "APP2001";

    private const string DbContextFullName = "Microsoft.EntityFrameworkCore.DbContext";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "DbContext used where a DbContextFactory is expected",
        messageFormat: "Constructor of '{0}' takes a DbContext — use IDbContextFactory<T> instead",
        category: "Architecture",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        if (context.Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } ctor)
        {
            return;
        }

        if (ctor.Parameters.Any(p => InheritsFrom(p.Type, DbContextFullName)))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, ctor.Locations[0], ctor.ContainingType.Name));
        }
    }

    private static bool InheritsFrom(ITypeSymbol? type, string fullName)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.ToDisplayString() == fullName)
            {
                return true;
            }
        }

        return false;
    }
}
