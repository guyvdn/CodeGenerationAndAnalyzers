using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MyApp.Analyzers;

/// <summary>
/// APP1001 — flags identifiers containing the common typo "Adress"
/// (should be "Address"). Paired with TypoCodeFixProvider (MyApp.CodeFixes) for a
/// one-click rename.
///
/// A simple rule, but it holds for everyone — humans and AI agents alike. An
/// instruction in a README can be ignored; a build warning cannot (and this one
/// is promoted to a build error in the demo project).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypoAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "APP1001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Possible typo in identifier",
        messageFormat: "Identifier '{0}' contains 'Adress' — did you mean 'Address'?",
        category: "Naming",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol,
            SymbolKind.Property, SymbolKind.Field, SymbolKind.Method, SymbolKind.NamedType);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;

        // Skip compiler-generated property accessors (get_/set_).
        if (symbol is IMethodSymbol { AssociatedSymbol: IPropertySymbol })
        {
            return;
        }

        if (symbol.Name.IndexOf("Adress", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, symbol.Locations[0], symbol.Name));
        }
    }
}
