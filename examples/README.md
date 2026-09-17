# CodeGenDemo — a working analyzers & source-generator solution

A real, buildable companion to the talk *"Source generators & analyzers in the
AI era"*. Everything is genericized (`MyApp.*`,
diagnostic prefix `APP####`) so it can be shared publicly.

> Ships in a **deliberately broken** state: one analyzer diagnostic (`APP1001`)
> is promoted to a build error, so you can fix it live on stage.

## Projects

| Project | Target | Role |
|---------|--------|------|
| `MyApp.CodeGen` | `netstandard2.0` | Incremental **source generator** (`IIncrementalGenerator`) — pipeline in `EnumGenerator.cs`, template in `EnumBuilder.cs` |
| `MyApp.Analyzers` | `netstandard2.0` | Three **analyzers**: `APP1001`, `APP2001`, `APP3001` |
| `MyApp.CodeFixes` | `netstandard2.0` | **Code fix** for `APP1001` (separate assembly — RS1038) |
| `MyApp.Demo` | `net10.0` | Console app that **consumes** all three as analyzers |
| `MyApp.Tooling.Tests` | `net10.0` | **Tests** for the generator, the three analyzers and the code fix |

The generator injects the `[GeneratedEnum]` attribute into the consuming
compilation (post-initialization output), so the demo needs no runtime reference
to the tooling — the three tooling projects are referenced purely as analyzers.

## Build & run

```powershell
# from examples/
dotnet build CodeGenDemo.slnx      # FAILS on purpose: error APP1001 in Customer.cs
dotnet run --project src/MyApp.Demo
```

Requires the .NET 10 SDK. The tooling targets `netstandard2.0` and pins Roslyn
`4.8.0`, which the .NET 10 compiler loads fine.

## Tests

```powershell
# from examples/
dotnet test tests/MyApp.Tooling.Tests
```

Run the **test project**, not the solution: `dotnet test CodeGenDemo.slnx` builds
`MyApp.Demo`, which fails on purpose (`APP1001`). `MyApp.Tooling.Tests` therefore
does not reference `MyApp.Demo` — it links in `Abstractions/*.cs` instead, so it
uses the real `SmartEnum<T>` and `EntityBase` without depending on a project that
is meant to be red.

| Tests | What they pin down |
|---|---|
| `CodeGen/EnumGeneratorTests` | The emitted text per `EnumType`: base list, `[JsonConverter]`, both converters, the namespace, and that the injected `EnumType` keeps the member order the generator unboxes against |
| `CodeGen/EnumGeneratorCachingTests` | The incremental half — an unrelated edit re-runs nothing, a changed backing type does, and the model has value equality (the reason caching works at all) |
| `CodeGen/GeneratedConverterTests` | The generated code **emitted, loaded and executed**: JSON round-trips to the same instance, the EF converter maps both ways, `All` comes from the hand-written base |
| `Analyzers/*` | Per rule: what fires, what stays quiet, the exact reported span, and — for `APP3001` — that a compilation without the marker type registers nothing |
| `CodeFixes/TypoCodeFixProviderTests` | The fix's title, the code it produces, that it reaches references in other documents, and that the fixed code no longer reports `APP1001` |

There is no `Microsoft.CodeAnalysis.Testing` dependency: `Infrastructure/` builds
the compilations from the test host's own reference set, so nothing is downloaded
at test time and the analyzers run against the exact framework the demo targets.

## The two things worth showing

### 1. The generator is load-bearing (not cosmetic)

`src/MyApp.Demo/Priority.cs` is all a developer writes:

```csharp
[GeneratedEnum(EnumType.Int)]
public sealed partial class Priority
{
    public required string Name { get; init; }
    public static Priority Low  { get; } = new() { Value = 1, Name = "Low" };
    public static Priority High { get; } = new() { Value = 2, Name = "High" };
}
```

That class has no base type and no `Value` property. The generator emits the
**base list** — `: IntEnum<Priority>`, picked from the `EnumType` — and the base
is where the typed `Value` and the `All` lookup come from. Change the attribute
to `EnumType.String` and the base becomes `StringEnum<Priority>`, `Value` becomes
a `string`, and both converters follow, with no other edits. Open the generated
file after a build:

```
obj/Debug/net10.0/generated/MyApp.CodeGen/MyApp.CodeGen.EnumGenerator/Priority.g.cs
```

Delete the `[GeneratedEnum]` attribute and the class stops compiling — proof the
generation is doing real work, not decoration.

### 1b. The converters — the boilerplate you'd otherwise copy-paste

The same generator also emits, per annotated class, the code the slides show:

| Generated | What it does |
|---|---|
| `: IntEnum<Priority>` | The base list — brings in the typed `Value` and `All` |
| `[JsonConverter(typeof(PriorityJsonConverter))]` | Wires the JSON converter up — no `JsonSerializerOptions` plumbing at the call site |
| `PriorityJsonConverter : JsonConverter<Priority>` | System.Text.Json: serializes as the bare `int`, round-trips to the **same instance** |
| `PrioritySqlConverter : ValueConverter<Priority, int>` | EF Core: `Value` in the column, the member back out |

All of it follows the `EnumType`: flip `Int` → `String` and the base type, the
wire format, the reader call and the EF column type change together (strings also
switch to case-insensitive matching). That's the argument for generating instead
of copy-pasting — one template, N enums, always in sync.

**`All` is *not* generated.** It lives in the hand-written
`src/MyApp.Demo/SmartEnum.cs`: `SmartEnum<TEnum>` keeps a static list and each
member **registers itself from the constructor**, so adding a member to
`Priority.cs` needs no generator change at all. `All` forces
`RuntimeHelpers.RunClassConstructor` first — without it a concurrent first reader
can see a non-empty but incomplete list, which shows up as a "no matching
element" from the `Single(...)` in exactly these converters — and hands back a
snapshot rather than the live list. That split is the point: **generate the
wiring, hand-write the mechanism.**

`Program.cs` exercises both converters:

```
JSON             : 2 -> High (same instance: True)
SQL column       : 1 -> Low
```

**Where the code lives.** `EnumGenerator.cs` is the pipeline and nothing else —
it matches the snippet on the slide line for line. The template is next door in
`EnumBuilder.cs`, behind a `BuildCode()` extension on the model. Adding a rule (a
third serializer, a different EF mapping) is a one-file change there, which is
exactly the argument against letting an agent copy-paste converters into N files.

### 2. The live fix (the breaking build)

`src/MyApp.Demo/Customer.cs` contains a typo:

```csharp
public string Adress { get; init; } = "";   // APP1001 -> build ERROR
```

`APP1001` is a *warning*, but `MyApp.Demo.csproj` has
`<WarningsAsErrors>APP1001</WarningsAsErrors>`, so the build **fails**. This is the
punchline: an AI agent can ignore a README, but it cannot ignore a red build.

**Live fix, two ways:**
- Type the rename: `Adress` → `Address`. Rebuild → green.
- Or use the light bulb 💡 *"Rename to 'Address'"* (from `MyApp.CodeFixes`) in
  Visual Studio / Rider / VS Code, which renames the declaration and every
  reference via `Renamer`.

**Bonus:** delete `sealed` from `Customer` to make `APP3001` fire too (entities
deriving from `EntityBase` must be sealed) — shows the fast, compilation-start
analyzer pattern in action.

## Why the code fix is a separate project

`CodeFixProvider` lives in `Microsoft.CodeAnalysis.Workspaces`. Putting that
reference in an analyzer assembly trips analyzer rule **RS1038** ("compiler
extensions should be implemented in assemblies with compiler-provided
references"). So `MyApp.Analyzers` stays Workspaces-free and the fix lives in
`MyApp.CodeFixes`. All three are wired into the demo with:

```xml
<ProjectReference Include="..." OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## Performance (the part an agent won't feel)

Analyzers are injected into every project and run on every build **and**
continuously in the IDE, across the whole solution. Compare the two styles:

- `MarkerTypeAnalyzer` (`APP3001`) — the **fast** pattern: gate scope once in
  `RegisterCompilationStartAction`, resolve the marker type once with
  `GetTypeByMetadataName`, compare with `SymbolEqualityComparer.Default`.
- `DbContextAnalyzer` (`APP2001`) — the older string-based style
  (`ToDisplayString()` per node), kept for contrast.

See `../skills/authoring-roslyn-tools/SKILL.md` for the full checklist.
