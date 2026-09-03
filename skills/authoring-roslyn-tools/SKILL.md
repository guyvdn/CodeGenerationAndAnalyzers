---
name: authoring-roslyn-tools
description: >
  Use when creating, authoring, or changing a Roslyn analyzer, code fix, or
  incremental source generator in a .NET codebase. Covers the required
  performance checklist, because analyzers are injected into every project and
  run solution-wide on every build AND continuously in the IDE, and a careless
  one degrades build time and IDE responsiveness for the whole team. Also use
  when deciding whether to generate code instead of duplicating it.
---

# Authoring Roslyn analyzers & source generators

Analyzers and source generators let the **compiler** do two jobs an AI agent does
badly: write repetitive code exactly once (generators) and enforce rules that
cannot be ignored (analyzers). This skill is the how-to plus the non-negotiable
performance rules.

> **Why performance is a required review item:** analyzers are typically injected
> into *every* project (e.g. via `Directory.Build.props`) and run on *every build*
> and *continuously in the IDE*. A slow analyzer runs across the whole solution on
> every keystroke — taxing IDE responsiveness and build time for everyone. An agent
> does not feel IDE lag; it only sees "compiles / doesn't compile", so it will
> happily ship a correct-but-slow analyzer unless told otherwise.

## When to generate vs. let an agent write it

- **Generate** (source generator) when the same shape repeats across many types:
  converters, DTOs, boilerplate interfaces, mappers. One attribute becomes the
  single source of truth; duplication drifts and multiplies maintenance.
- **Enforce** (analyzer) when a convention must hold everywhere: naming,
  architecture rules, banned APIs, required registrations. An instruction in a
  README can be ignored; a build error cannot.
- The generator/analyzer *code itself* is small and standardized — fine to let an
  agent draft it. Your job is the **choice** to generate/enforce and the
  **performance** of the result.

## Analyzer performance checklist (always review)

- **Gate scope once per compilation.** If the analyzer only targets some
  assemblies/files, do the check inside `RegisterCompilationStartAction(...)` and
  register the per-symbol/per-node action **only** when it matches. Non-target
  compilations then do **zero** per-node work. NEVER scope-check-then-bail inside a
  bare `RegisterSymbolAction`/`RegisterSyntaxNodeAction` — it still fires for every
  node in the whole solution.
- **Resolve marker symbols once, compare by symbol.** Resolve types/attributes via
  `compilation.GetTypeByMetadataName(...)` in the start action and compare with
  `SymbolEqualityComparer.Default`. NEVER call `ISymbol.ToDisplayString()` (or
  string-compare display names) per node/member — it allocates a string every call.
- **Always** `EnableConcurrentExecution()` and
  `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` in `Initialize`.
- **Avoid redundant walks.** Use `GetMembers(name)` for a known name; collect what
  you need in a single pass rather than re-scanning.
- **NEVER store per-compilation state in analyzer instance fields** (RS1008) — pass
  resolved symbols through the `CompilationStartAction` closure instead.
- **Keep code fixes in a separate project** from the analyzer — the
  `Microsoft.CodeAnalysis.Workspaces` reference trips RS1038 if placed in the
  analyzer assembly.

### Fast analyzer skeleton

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MyAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        "APP2001", "Title", "Message '{0}'", "Category",
        DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnStart);
    }

    private static void OnStart(CompilationStartAnalysisContext context)
    {
        // Resolve markers ONCE. Non-matching compilations register nothing → zero work.
        var marker = context.Compilation.GetTypeByMetadataName("MyApp.Marker");
        if (marker is null) return;

        context.RegisterSymbolAction(
            ctx => Analyze(ctx, marker), SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext ctx, INamedTypeSymbol marker)
    {
        if (SymbolEqualityComparer.Default.Equals(ctx.Symbol, marker))
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Symbol.Locations[0], ctx.Symbol.Name));
    }
}
```

## Source generator performance checklist

- **Use `IIncrementalGenerator`**, never the legacy `ISourceGenerator`.
- **Drive the pipeline with `ForAttributeWithMetadataName`** so it runs only on
  nodes carrying your attribute, not every node in the tree.
- **Make the pipeline model an equatable value type** (`readonly record struct`
  with real value equality). The incremental pipeline caches on equality and
  re-generates only when the model actually changes — an unequatable model
  defeats caching and re-runs on every keystroke.
- **Keep the `transform` step pure and small** — extract just the data you need
  (names, namespace, options); do string building later in the output step.

### Incremental generator skeleton

```csharp
[Generator]
internal sealed class MyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider.ForAttributeWithMetadataName(
            "MyApp.GeneratedThingAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (ctx, _) => ToModel(ctx));   // pure, returns a record struct

        context.RegisterSourceOutput(models,
            static (ctx, m) => ctx.AddSource($"{m.Name}.g.cs", Build(m)));
    }
}
```

## Enforcement layering (make rules stick)

Treat instructions and analyzers as a funnel, softest → hardest:

1. Broad prose instructions (`copilot-instructions.md`, `CLAUDE.md`) — *can be ignored*.
2. Path-scoped rule files (`.claude/rules/*.md`) — loaded deterministically on matching edits.
3. Analyzer diagnostic — visible live in the IDE.
4. `TreatWarningsAsErrors=true` in CI — the build fails; nobody, human or agent, gets past it.

Put the hard MUST/NEVER constraints where the compiler enforces them, not only in docs.

## Common mistakes to catch in review

- Scope check inside the per-node action instead of `CompilationStartAction`.
- `ToDisplayString()` / display-name string compares per node.
- Non-equatable generator model (class instead of `record struct`, or reference
  fields without value equality) → cache misses, re-runs every keystroke.
- Legacy `ISourceGenerator`, or a generator that walks all syntax instead of
  `ForAttributeWithMetadataName`.
- Code fix living in the analyzer assembly (RS1038).
- Per-compilation state in analyzer fields (RS1008).
