# Source generators & analyzers in the AI era

AI agents write code fast — and duplicate it just as fast. Let source generators
write the repetition and analyzers enforce your rules, so a guideline becomes a
build error nobody can ignore.

An agent can ignore a README. It cannot ignore a red build.

## Contents

| Path | What it is |
|------|------------|
| `deck/` | **The [reveal.js](https://revealjs.com) deck** — custom interface, five live demo panels, speaker notes. See [`deck/README.md`](deck/README.md) |
| `examples/` | `CodeGenDemo` — a real, buildable analyzers & source-generator solution, with tests for the generator, analyzers and code fix |
| `skills/authoring-roslyn-tools/` | An AI skill that teaches an agent to write correct, *fast* Roslyn tooling |

The four parts each own an accent colour, there's a chapter rail and a
slash-to-jump palette, and five slides are interactive rather than described —
the generator actually emits its file, the light bulb actually applies the code
fix, and the slow analyzer actually loses the race.

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

`APP1001` is promoted to a build **error** in the demo, so it can be fixed live on
stage if there's time — the *From error to fix* slide simulates it either way.

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
./start-deck.ps1                   # installs deps on first run, serves on http://localhost:8081
```

Keys: <kbd>?</kbd> help, <kbd>/</kbd> jump to a slide, <kbd>B</kbd> run the demo on
an interactive slide, <kbd>R</kbd> reset it, <kbd>D</kbd> draw, <kbd>S</kbd> speaker
notes. reveal.js is installed locally, so it presents with no network.

## Running the demo

Requires the .NET 10 SDK.

```powershell
cd examples
dotnet build CodeGenDemo.slnx      # FAILS on purpose: error APP1001 in Customer.cs
dotnet run --project src/MyApp.Demo
dotnet test tests/MyApp.Tooling.Tests
```

See [`examples/README.md`](examples/README.md) for the full walkthrough, including
what to demo live and why the code fix lives in its own assembly.

## License

MIT — see [LICENSE](LICENSE).
