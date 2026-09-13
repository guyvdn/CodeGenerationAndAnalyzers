# CodeGenDemo — a working analyzers & source-generator solution

A real, buildable companion to the talk *"Source generators & analyzers in the
AI era"*. Everything is genericized (`MyApp.*`,
diagnostic prefix `APP####`) so it can be shared publicly.

> Ships in a **deliberately broken** state: one analyzer diagnostic (`APP1001`)
> is promoted to a build error, so you can fix it live on stage.

## Projects

| Project | Target | Role |
|---------|--------|------|
| `MyApp.CodeGen` | `netstandard2.0` | Incremental **source generator** (`IIncrementalGenerator`) |
| `MyApp.Analyzers` | `netstandard2.0` | Three **analyzers**: `APP1001`, `APP2001`, `APP3001` |
| `MyApp.CodeFixes` | `netstandard2.0` | **Code fix** for `APP1001` (separate assembly — RS1038) |
| `MyApp.Demo` | `net10.0` | Console app that **consumes** all three as analyzers |

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

The `Value` property doesn't exist in that file — the generator emits it, **typed
by the `EnumType`** (`int` here; change to `EnumType.String` and it becomes a
`string`, no other edits). Open the generated file after a build:

```
obj/Debug/net10.0/generated/MyApp.CodeGen/MyApp.CodeGen.EnumGenerator/Priority.g.cs
```

Delete the `[GeneratedEnum]` attribute and the class stops compiling — proof the
generation is doing real work, not decoration.

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
