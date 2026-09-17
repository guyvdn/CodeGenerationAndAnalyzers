using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MyApp.Analyzers;

/// <summary>
/// APP3001 — the FAST pattern to copy for new analyzers. Entities deriving from a
/// marker base type (MyApp.Demo.Abstractions.EntityBase) must be <c>sealed</c>.
///
/// Performance wins:
///  1. Gate scope ONCE per compilation in RegisterCompilationStartAction. If the
///     marker type isn't referenced, we register no per-symbol action, so
///     non-target compilations do ZERO per-node work.
///  2. Resolve the marker ONCE via GetTypeByMetadataName and compare with
///     SymbolEqualityComparer.Default — never ToDisplayString() per node.
///  3. Resolved symbols flow through the closure — no per-compilation state in
///     analyzer fields (which would trip RS1008).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MarkerTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "APP3001";

    private const string MarkerMetadataName = "MyApp.Demo.Abstractions.EntityBase";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Entity must be sealed",
        messageFormat: "Entity '{0}' derives from EntityBase and must be declared 'sealed'",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var marker = context.Compilation.GetTypeByMetadataName(MarkerMetadataName);
        if (marker is null)
        {
            return; // marker not referenced -> zero per-symbol work
        }

        context.RegisterSymbolAction(ctx => Analyze(ctx, marker), SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol marker)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type is { TypeKind: TypeKind.Class, IsSealed: false, IsAbstract: false } &&
            DerivesFrom(type, marker))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name));
        }
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol marker)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, marker))
            {
                return true;
            }
        }

        return false;
    }
}
