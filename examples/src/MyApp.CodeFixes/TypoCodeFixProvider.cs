using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;

namespace MyApp.CodeFixes;

/// <summary>
/// One-click fix for APP1001: rename an identifier containing "Adress" to the
/// corrected spelling ("Address"). Surfaced in the IDE light bulb and applicable
/// by `dotnet format` — the fix IS the correct form, so an agent doesn't guess.
///
/// This lives in its OWN assembly (references Microsoft.CodeAnalysis.Workspaces);
/// putting that reference in the analyzer project would trip RS1038.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TypoCodeFixProvider)), Shared]
public sealed class TypoCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("APP1001");

    // "Fix all in document/project/solution" for free.
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent;
        if (node is null)
        {
            return;
        }

        var model = await context.Document
            .GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var symbol = model?.GetDeclaredSymbol(node, context.CancellationToken);
        if (symbol is null)
        {
            return;
        }

        var newName = symbol.Name
            .Replace("Adress", "Address")
            .Replace("adress", "address");

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Rename to '{newName}'",
                createChangedSolution: c => RenameAsync(context.Document, symbol, newName, c),
                equivalenceKey: "RenameToAddress"),
            diagnostic);
    }

    private static async Task<Solution> RenameAsync(
        Document document, ISymbol symbol, string newName, CancellationToken ct)
    {
        var solution = document.Project.Solution;

        // Renames the declaration AND every reference across the solution.
        // SymbolRenameOptions is the modern (Roslyn 4.x) options type.
        return await Renamer
            .RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, ct)
            .ConfigureAwait(false);
    }
}
