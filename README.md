# Transform Your .NET Development

## Code Generation & Analyzers in Action!

A talk about the compiler as your fastest — and strictest — teammate in the AI era.
AI agents write code faster than ever, but *faster* is not the same as *better*.
Two forces of the compiler make the difference: **source generators** let the compiler
write the repetition, **analyzers** let the compiler enforce your rules.

An agent can ignore a README. It cannot ignore a red build.

## Contents

| Path | What it is |
|------|------------|
| `slides.md` | The [reveal-md](https://github.com/webpro/reveal-md) presentation (speaker notes included) |
| `slides.pdf` | Exported PDF of the deck |
| `examples/` | `CodeGenDemo` — a real, buildable analyzers & source-generator solution |
| `skills/authoring-roslyn-tools/` | An AI skill that teaches an agent to write correct, *fast* Roslyn tooling |

## What the talk covers

### Part 1 — Source Generators
One attribute (`[GeneratedEnum]`) replaces a pile of hand-written boilerplate. The
generator is load-bearing, not cosmetic: remove the attribute and the code stops
compiling. Compare that with an agent duplicating the same shape ten times and
letting the copies drift.

### Part 2 — Analyzers
Instructions vs. enforcement. Three diagnostics in the demo solution:

| ID | Rule |
|----|------|
| `APP1001` | Naming convention — with a code fix (light bulb → *Rename to 'Address'*) |
| `APP2001` | Architecture rule, older string-comparison style (kept for contrast) |
| `APP3001` | Architecture rule, fast symbol-comparison style |

`APP1001` is promoted to a build **error** in the demo, so it can be fixed live on stage.

### Part 3 — Performance
Analyzers run on every build *and* continuously in the IDE, across the whole
solution. The talk contrasts the slow pattern (`ToDisplayString()` per node) with
the fast one (gate scope in `RegisterCompilationStartAction`, resolve types once
with `GetTypeByMetadataName`, compare with `SymbolEqualityComparer.Default`), and
ends in a checklist.

### Part 4 — The AI Skill
Package that checklist as a skill so the agent applies it without being asked.
See [`skills/authoring-roslyn-tools/SKILL.md`](skills/authoring-roslyn-tools/SKILL.md).

## Running the deck

```powershell
npm install -g reveal-md
reveal-md slides.md --port 8080 --watch
```

## Running the demo

Requires the .NET 10 SDK.

```powershell
cd examples
dotnet build CodeGenDemo.slnx      # FAILS on purpose: error APP1001 in Customer.cs
dotnet run --project src/MyApp.Demo
```

See [`examples/README.md`](examples/README.md) for the full walkthrough, including
what to demo live and why the code fix lives in its own assembly.

## License

MIT — see [LICENSE](LICENSE).
