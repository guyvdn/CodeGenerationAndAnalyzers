---
title: Transform Your .NET Development
description: run with reveal-md.cmd slides.md
theme: dracula
highlightTheme: monokai
revealOptions:
  transition: slide
  controls: true
  progress: true
---

<style>
  .reveal {
    font-size: 2.1em;
  }

  .reveal blockquote {
    font-size: 0.8em;
    line-height: 1.35;
  }

  .reveal blockquote p {
    margin: 0;
  }

  .reveal pre {
    font-size: 0.5em;
    line-height: 1.25;
    width: 100%;
  }

  .reveal table {
    font-size: 0.72em;
  }

  .reveal .small {
    font-size: 0.7em;
  }
</style>

# Transform Your .NET Development

## Code Generation & Analyzers in Action!

The compiler as your fastest — and strictest — teammate in the AI era.

Note:
Welcome. In 15-20 minutes I'll show why the compiler becomes more important in the
AI era, not less. Two techniques: source generators and analyzers. Every example
comes from a real .NET codebase, made generic.

---

## The new reality

<ul>
<li class="fragment fade-in-then-semi-out">AI agents write code <strong>faster than ever</strong></li>
<li class="fragment fade-in-then-semi-out">But <em>faster</em> is not the same as <em>better</em></li>
<li class="fragment fade-in-then-semi-out">Two forces of the <strong>compiler</strong> make the difference:
<ul>
<li class="fragment fade-in-then-semi-out"><strong>Source Generators</strong> — let the compiler write the repetition</li>
<li class="fragment fade-in-then-semi-out"><strong>Analyzers</strong> — let the compiler enforce your rules</li>
</ul>
</li>
</ul>

Note:
The throughline: let the machine do what the machine is good at. The compiler is
deterministic, fast and tireless. An agent is not.

---

## The problem with "fast"

<ul>
<li class="fragment fade-in-then-semi-out">Agents duplicate boilerplate in <strong>seconds</strong></li>
<li class="fragment fade-in-then-semi-out">Copy-paste at scale → <strong>drift</strong>, inconsistency, subtle bugs</li>
<li class="fragment fade-in-then-semi-out">10 variants of "almost the same" → maintenance hell</li>
<li class="fragment fade-in-then-semi-out">More code = more surface for mistakes</li>
</ul>

<blockquote class="fragment fade-in"><p>AI makes it cheap to do the <em>wrong</em> things — at scale.</p></blockquote>

Note:
The temptation is strong: "the agent does it in a second anyway." But ten
duplicated converters are ten places that can drift out of sync.

---

## Two allies of the compiler

| | Source Generator | Analyzer |
|---|---|---|
| **Role** | Writes code | Guards code |
| **When** | At build time | At build time **and** in the IDE |
| **Payoff** | One source, zero duplication | Rules no one can ignore |
| **Output** | `.g.cs` files | Warnings / build errors |

Note:
Generators reduce the amount of hand-written code. Analyzers guard the quality of
the code that remains. Together: less code, stronger guarantees.

---

<!-- .slide: data-background="#1a1a2e" -->
# Part 1

## Source Generators

*Let the compiler write the repetition*

---

## The idea: one attribute

```csharp
[GeneratedEnum(EnumType.Int)]
public sealed partial class Priority
{
    public required string Name { get; init; }

    public static Priority Low  { get; } = new() { Value = 1, Name = "Low" };
    public static Priority High { get; } = new() { Value = 2, Name = "High" };
}
```

<p class="fragment fade-in">You write the <strong>intent</strong>. The compiler writes the rest.</p>

Note:
This is the only code the developer writes: a partial class with an attribute and
the values. Type-safe, no magic strings.

---

## What the compiler produces

```csharp
// Priority.g.cs  —  auto-generated, never touched by hand
[JsonConverter(typeof(PriorityJsonConverter))]
public sealed partial class Priority : IntEnum<Priority>;

public class PriorityJsonConverter : JsonConverter<Priority>
{
    public override Priority Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        => Priority.All.Single(x => x.Value == r.GetInt32());
    // + Write(...)
}

public class PrioritySqlConverter() : ValueConverter<Priority, int>(
    convertToProviderExpression:   x => x.Value,
    convertFromProviderExpression: x => Priority.All.Single(a => a.Value == x)
);
```

<p class="fragment fade-in">System.Text.Json <strong>+</strong> Newtonsoft <strong>+</strong> EF converter — for <em>every</em> enum, for free.</p>

Note:
That's dozens of lines per enum: two JSON converters and an EF value converter.
Four variants (int/string/guid/byte). You never want to write this by hand — nor
have an agent copy-paste it.

---

## Generator vs. AI duplication

| | Source Generator | Agent that copies |
|---|---|---|
| **Source of truth** | One (the attribute) | Spread across N files |
| **Sync** | Always, automatic | Manual, drifts apart |
| **Determinism** | Identical on every build | Varies per prompt |
| **Cost per new enum** | 0 tokens, 0 seconds | Tokens + review, every time |
| **Add a new rule** | Change 1 place | Chase down N places |

<blockquote class="fragment fade-in"><p>The generator scales with one attribute. Duplication scales with your maintenance burden.</p></blockquote>

Note:
The real argument. Add a third JSON library tomorrow? With the generator you change
one template and every enum gets it. With duplication you hunt down every copy.

---

## The generator is this small

```csharp
[Generator]
internal sealed class EnumGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var enums = context.SyntaxProvider.ForAttributeWithMetadataName(
            GeneratedEnumAttribute.Name,
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (ctx, _) => GetEnumToGenerate(ctx));

        context.RegisterSourceOutput(enums,
            static (ctx, e) => ctx.AddSource($"{e.ClassName}.g.cs", e.BuildCode()));
    }
}
```

<p class="fragment fade-in">This kind of code is <strong>fine to let an agent write</strong> — it's small, standardized, and the compiler tells you immediately whether it's right.</p>

Note:
Deliberately stopping here: I will NOT walk through building a generator line by
line. An agent does that trivially today. The value is in the choice to generate,
and — coming up — doing it correctly and performantly.

---

<!-- .slide: data-background="#1a1a2e" -->
# Part 2

## Analyzers

*Let the compiler enforce your rules*

---

## Instructions vs. enforcement

<ul>
<li class="fragment fade-in-then-semi-out">Conventions in a <code>README</code>, <code>CLAUDE.md</code> or <code>copilot-instructions</code></li>
<li class="fragment fade-in-then-semi-out">... get read <strong>when it's convenient</strong></li>
<li class="fragment fade-in-then-semi-out">An agent can <strong>ignore, forget or "creatively interpret"</strong> an instruction</li>
</ul>

<blockquote class="fragment fade-in"><h2>AI agents can ignore instructions.<br>Build errors, they can't.</h2></blockquote>

Note:
This is the core of part 2. A prompt is a suggestion. An analyzer error is a hard
wall. The agent can only move on once the code compiles.

---

## Analyzer 1 — a naming convention

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TypoAnalyzer : DiagnosticAnalyzer   // APP1001
{
    // "Adress" -> did you mean "Address"?
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol,
            SymbolKind.Property, SymbolKind.Field, SymbolKind.NamedType);
    }

    static void AnalyzeSymbol(SymbolAnalysisContext ctx)
    {
        if (ctx.Symbol.Name.Contains("Adress", StringComparison.OrdinalIgnoreCase))
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Symbol.Locations[0], ctx.Symbol.Name));
    }
}
```

<p class="fragment fade-in">Small, but it enforces consistency for <strong>everyone</strong> — human and agent alike.</p>

Note:
Deliberately a simple example. Even a trivial rule is valuable if it applies
automatically and everywhere. Note: this is the "old", string-based style — fine
for its scope, but we'll see the faster variant shortly.

---

## From error to fix

<ul>
<li class="fragment fade-in-then-semi-out">An analyzer <strong>points at the problem</strong> — a <strong>code fix</strong> solves it</li>
<li class="fragment fade-in-then-semi-out">One click via the light bulb 💡, or automatically via <code>dotnet format</code></li>
<li class="fragment fade-in-then-semi-out">The fix <strong>is</strong> the correct form — perfect for an agent, no guesswork</li>
</ul>

```text
  public string Adress { get; set; }
                ~~~~~~   APP1001: did you mean 'Address'?
  💡 Rename to 'Address'          ← one click
```
<!-- .element: class="fragment fade-in" -->

Note:
The analyzer and the code fix are a pair: one detects, the other repairs. For an
agent this is ideal — the toolbox hands over the correct form directly.

---

## The code fix (APP1001)

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class TypoCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["APP1001"];

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var node = root!.FindToken(context.Span.Start).Parent!;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Rename to 'Address'",
                createChangedSolution: c => RenameAsync(context.Document, node, c),
                equivalenceKey: "RenameToAddress"),
            context.Diagnostics[0]);
    }
}
```

<p class="fragment fade-in">Code fixes belong in a <strong>separate project</strong> — they reference <code>Microsoft.CodeAnalysis.Workspaces</code> (which the analyzer itself must not: <strong>RS1038</strong>).</p>

Note:
Per the Microsoft docs, RegisterCodeFixesAsync lives in
Microsoft.CodeAnalysis.Workspaces.dll. That's why the fix belongs in its own
assembly: put it next to the analyzer and the Workspaces reference trips RS1038.
I will NOT walk the API line by line here — an agent does that trivially.

---

<!-- .slide: data-background="#2e1a2e" -->
## 🔴 Live demo

<ul>
<li class="fragment fade-in-then-semi-out">A <code>Customer</code> with an <code>Adress</code> typo → <strong>the build fails</strong> (APP1001 as an error)</li>
<li class="fragment fade-in-then-semi-out">I fix it live: 💡 <strong>Rename to 'Address'</strong> → build goes green</li>
<li class="fragment fade-in-then-semi-out">And the generated <code>Priority.g.cs</code> — the compiler wrote the <code>Value</code></li>
</ul>

```text
  examples/  ->  dotnet build   ❌ error APP1001
                 fix + rebuild   ✅ High (value 2)
```
<!-- .element: class="fragment fade-in" -->

Note:
Switch to the IDE, open examples/CodeGenDemo.slnx. Show: (1) the red build on
Customer.Adress, (2) the code fix via the light bulb, (3) Priority.g.cs under
obj/.../generated with the generated Value. Fall back to these slides if the demo
hiccups. Optional: remove 'sealed' from Customer to make APP3001 fire.

---

## Analyzer 2 — an architecture rule

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DbContextAnalyzer : DiagnosticAnalyzer   // APP2001
{
    // "Constructor takes a DbContext -> use IDbContextFactory"
    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
    }

    static void AnalyzeSymbol(SymbolAnalysisContext ctx)
    {
        if (ctx.Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } c &&
            c.Parameters.Any(p => p.Type.Is("Microsoft.EntityFrameworkCore.DbContext")))
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, c.Locations[0], c.ContainingType.Name));
    }
}
```

<p class="fragment fade-in">A team agreement becomes a <strong>build error</strong> — no more review discussion needed.</p>

Note:
This is a real architecture rule: don't inject a DbContext, inject a factory
(concurrency, lifetime). You used to catch this in code review. Now the compiler
catches it — before the PR, for the agent too.

---

## From suggestion to guarantee

Layered defence — each layer stricter than the last:

<ol>
<li class="fragment fade-in-then-semi-out"><strong><code>copilot-instructions.md</code></strong> — broad context <em>(can be ignored)</em></li>
<li class="fragment fade-in-then-semi-out"><strong><code>.claude/rules/</code></strong> — path-scoped MUST/NEVER <em>(always loaded on edit)</em></li>
<li class="fragment fade-in-then-semi-out"><strong>Analyzer</strong> — warning in the IDE <em>(visible, live)</em></li>
<li class="fragment fade-in-then-semi-out"><strong><code>TreatWarningsAsErrors</code></strong> in CI — <strong>build fails</strong> <em>(cannot be bypassed)</em></li>
</ol>

<blockquote class="fragment fade-in"><p>Instructions persuade. The analyzer + CI <strong>guarantee</strong>.</p></blockquote>

Note:
The trick isn't choosing between instructions and analyzers — it's a funnel. The
agent gets the soft hint first, but the hard boundary is the red build.

---

<!-- .slide: data-background="#4a1a1a" -->
# Part 3

## Performance

*The warning your agent doesn't feel*

---

## Why performance is critical

<ul>
<li class="fragment fade-in-then-semi-out">Analyzers are injected into <strong>every</strong> project</li>
<li class="fragment fade-in-then-semi-out">They run on <strong>every build</strong> — and <strong>continuously in the IDE</strong></li>
<li class="fragment fade-in-then-semi-out">A slow analyzer runs on <strong>every keystroke</strong>, across the whole solution</li>
</ul>

<blockquote class="fragment fade-in"><p>One sloppy analyzer taxes the IDE responsiveness and the build time of the <strong>entire team</strong>.</p></blockquote>

Note:
This is the hidden cost. Functionally a slow analyzer works perfectly — it doesn't
"fail". But it eats away at everyone's developer experience, every day.

---

## Slow vs. fast

```csharp [1-6|8-16]
// ❌ SLOW: runs for every node in the whole solution, allocates per node
context.RegisterSymbolAction(ctx =>
{
    if (ctx.Symbol.ToDisplayString() == "MyApp.Marker")   // string per node!
        ...
}, SymbolKind.NamedType);

// ✅ FAST: scope once per compilation, compare by symbol
context.RegisterCompilationStartAction(start =>
{
    var marker = start.Compilation.GetTypeByMetadataName("MyApp.Marker");
    if (marker is null) return;                 // irrelevant projects: 0 work

    start.RegisterSymbolAction(ctx => {
        if (SymbolEqualityComparer.Default.Equals(ctx.Symbol, marker)) ...
    }, SymbolKind.NamedType);
});
```

Note:
The two biggest mistakes: (1) scope-check inside the per-node action instead of
once per compilation, and (2) ToDisplayString() per node, which allocates a string
every time. Resolve markers once, compare by symbol.

---

## The checklist for fast analyzers

<ul>
<li class="fragment fade-in-then-semi-out"><strong>Scope once</strong> in <code>RegisterCompilationStartAction</code> — irrelevant projects do 0 work</li>
<li class="fragment fade-in-then-semi-out"><strong>Resolve marker types once</strong> via <code>GetTypeByMetadataName</code>, compare with <code>SymbolEqualityComparer</code></li>
<li class="fragment fade-in-then-semi-out"><strong>Never</strong> <code>ISymbol.ToDisplayString()</code> per node — it allocates every time</li>
<li class="fragment fade-in-then-semi-out"><code>EnableConcurrentExecution()</code> + <code>ConfigureGeneratedCodeAnalysis(None)</code></li>
<li class="fragment fade-in-then-semi-out"><strong>No</strong> per-compilation state in analyzer fields (RS1008)</li>
</ul>

Note:
These are the rules you bake into a skill, so your agent follows them every time.

---

## Generators must be fast too

<ul>
<li class="fragment fade-in-then-semi-out">Use <strong><code>IIncrementalGenerator</code></strong> — not the old <code>ISourceGenerator</code></li>
<li class="fragment fade-in-then-semi-out"><code>ForAttributeWithMetadataName</code> → the pipeline only runs on relevant nodes</li>
<li class="fragment fade-in-then-semi-out">Model as a <strong>value type / equatable record</strong> → the compiler caches and doesn't regenerate needlessly</li>
<li class="fragment fade-in-then-semi-out">Keep the <code>transform</code> step <strong>pure and small</strong></li>
</ul>

<blockquote class="fragment fade-in"><p>A non-incremental generator re-runs on every keystroke → IDE lag.</p></blockquote>

Note:
Incremental is the key. The equatable data model (readonly record struct) lets the
pipeline cache results and only recompute what actually changed.

---

## Make your agent aware of this

<ul>
<li class="fragment fade-in-then-semi-out">An agent <strong>doesn't feel IDE lag</strong> — it only sees "compiles / doesn't compile"</li>
<li class="fragment fade-in-then-semi-out">Without direction it writes a correct, but <strong>slow</strong> analyzer</li>
<li class="fragment fade-in-then-semi-out">Performance is an <strong>explicit review item</strong>, not an afterthought</li>
</ul>

<blockquote class="fragment fade-in"><p>The agent optimizes for what you hold it accountable to. Hold it accountable for performance.</p></blockquote>

Note:
This is exactly why we need a skill: give the performance knowledge to the agent
structurally, instead of hoping it knows.

---

<!-- .slide: data-background="#1a2e1a" -->
# Part 4

## The AI Skill

*Teach the agent to do it right from the start*

---

## What is a skill?

<ul>
<li class="fragment fade-in-then-semi-out">A reusable <strong>instruction package</strong> the agent loads for a matching task</li>
<li class="fragment fade-in-then-semi-out">Lives <strong>in the repo</strong>, in git → shared with the whole team and every agent</li>
<li class="fragment fade-in-then-semi-out">Captures the <strong>how</strong> and the <strong>pitfalls</strong> — once, for good</li>
</ul>

<blockquote class="fragment fade-in"><p>Don't re-explain it in every prompt. The knowledge lives next to the code.</p></blockquote>

Note:
A skill is markdown with a bit of frontmatter. The agent picks it automatically
based on the description. I'm shipping the full, generic skill alongside these slides.

---

## The skill in practice

```markdown
---
name: authoring-roslyn-tools
description: Use when creating or changing a Roslyn analyzer,
  code fix, or source generator. Covers the required perf checklist,
  because analyzers run solution-wide on every build and keystroke.
---

# Analyzers & generators — do it right

## Analyzer performance (always review)
- Gate scope once in RegisterCompilationStartAction
- Resolve marker types once (GetTypeByMetadataName), compare by symbol
- NEVER call ISymbol.ToDisplayString() per node
- EnableConcurrentExecution + ConfigureGeneratedCodeAnalysis(None)

## Generators
- IIncrementalGenerator + ForAttributeWithMetadataName
- Model = equatable readonly record struct (cacheable)
```

<p class="fragment fade-in">The full <code>SKILL.md</code> ships alongside these slides.</p>

Note:
This is the bridge: everything from part 3 gets locked in here structurally. The
agent reads it automatically the moment it touches an analyzer or generator.

---

## In summary

<ul>
<li class="fragment fade-in-then-semi-out"><strong>Generate &gt; duplicate</strong> — one source, zero drift, scales for free</li>
<li class="fragment fade-in-then-semi-out"><strong>Analyzers &gt; instructions</strong> — a build error can't be ignored by anyone</li>
<li class="fragment fade-in-then-semi-out"><strong>Let the agent write the code</strong> — but guard the <strong>choices</strong> and the <strong>performance</strong></li>
<li class="fragment fade-in-then-semi-out"><strong>Bake your rules into a skill</strong> — knowledge that travels with the code</li>
</ul>

<blockquote class="fragment fade-in"><p>In the AI era, the compiler is your most reliable teammate.</p></blockquote>

Note:
If you take away one thing: move truth and rules from "documentation that must be
read" to "behaviour the compiler enforces".

---

<!-- .slide: data-background="#1a1a2e" -->
# Thank you!

## Questions?

*Transform Your .NET Development: Code Generation & Analyzers in Action*

Note:
Thanks. The slides and the included SKILL.md are free to share.
