# The interactive deck

The talk, as a [reveal.js](https://revealjs.com) 5 deck built directly against
reveal.js rather than generated from Markdown, so it can carry a custom interface
and five interactive panels instead of static bullet slides.

```powershell
../start-deck.ps1              # installs deps on first run, serves on :8081
```

## What the custom interface adds

**Interface**

| | |
|---|---|
| Chapter rail (left) | The four parts, with a dot per slide in the current part. Click to jump. Labels appear on hover. |
| Accent per part | Each part owns a colour (violet → cyan → amber → rose → emerald). It tints the background glow, headings, rail, buttons and code highlights, so where you are in the talk is legible at a glance. |
| Command palette | <kbd>/</kbd> or <kbd>Ctrl</kbd>+<kbd>K</kbd>. Fuzzy-matches slide titles, shows part and slide number, marks the interactive ones. The way to answer an out-of-order question without arrowing through 30 slides. |
| Keyboard help | <kbd>?</kbd> |
| Annotation layer | <kbd>D</kbd> draws on the slide in the part's accent colour, <kbd>C</kbd> clears. Strokes are kept per slide. |
| Code stepping | Arrow keys walk highlighted regions of a code block (`data-steps` below). |

**Interactive panels** — each is driveable from the clicker: <kbd>B</kbd> runs it,
<kbd>R</kbd> resets it. Entering a slide always resets its panel, so a second
pass through the deck behaves like the first.

| Slide | What it does |
|---|---|
| Run the generator | Press *dotnet build* and the generated `Priority.g.cs` streams in line by line. Flip `EnumType.Int` → `String` → `Guid` and rebuild: the whole generated surface changes from one attribute argument. This is the argument for generation over duplication, made visible. |
| What duplication actually costs | Slider for the number of enum types. Boilerplate lines, places to change one rule, review surface and drift probability all move with it — duplication's curve is linear, the generator's is flat. |
| From error to fix | A fake editor with `Adress` under a red squiggle. Click the 💡, pick *Rename to 'Address'*: the code changes, the error list clears, the build flips to green. Also the safety net if the live IDE demo misbehaves. |
| From suggestion to guarantee | Sends an agent token down the four defence layers. It slips past both instruction layers, ignores the analyzer warning, and the CI build error stops it dead. |
| One keystroke, whole solution | Races the slow analyzer (`ToDisplayString()` per node, no scope gate) against the fast one, with live node / allocation / elapsed counters. |

The numbers in the calculator and the race are **illustrative, not benchmarked** —
the speaker notes on those slides say so, and so should you if asked. The shapes
are real; the digits are a model.

## Authoring

Content is plain HTML in `index.html`. Three deck-specific conventions:

**Code blocks** live in a `<script type="text/template">` inside a
`figure[data-code]`. Script raw text means C# generics need no HTML escaping, so
snippets can be pasted straight out of `../examples/`:

```html
<figure class="code-window" data-code data-lang="csharp"
        data-file="TypoAnalyzer.cs" data-tag="APP1001" data-steps="1-2|6-11">
  <script type="text/template">
public override ImmutableArray<string> FixableDiagnosticIds => ["APP1001"];
  </script>
</figure>
```

- `data-lang` — `csharp`, `markdown` or `text`
- `data-file` / `data-tag` — the window's filename and right-hand badge
- `data-steps` — `"1-2|6-11"` adds one reveal fragment per range; arrowing
  through them highlights that range and dims the rest

**Parts.** Every `<section>` carries `data-part="intro|gen|analyze|perf|skill"`,
which drives the accent and the rail. The part list itself is `PARTS` at the top
of `js/chrome.js`.

**Slide titles.** `data-title` on a section is what the palette and rail show;
without it they fall back to the slide's `h1`/`h2`.

## Files

| Path | What |
|---|---|
| `index.html` | All slide content, plus the `Reveal.initialize` call |
| `css/deck.css` | The theme. Tokens, a theme-application layer (reveal.css ships almost no typography — a theme file is what binds `--r-*` to selectors), deck components, the widgets, then the chrome. |
| `js/code.js` | `DeckCode` — the C#/markdown tokenizer, code-window rendering, `data-steps` fragments |
| `js/widgets.js` | `DeckWidgets` — the five panels, each registering `{ run, reset }` |
| `js/chrome.js` | `DeckChrome` — accent theming, rail, context readout, palette, help, annotation canvas, key bindings |

No build step and no third-party reveal plugins: only reveal's own `notes` and
`zoom`. reveal.js is installed locally rather than pulled from a CDN so the deck
presents with no network. Fonts come from Google Fonts with real local fallbacks
(Segoe UI / Cascadia Code), so an offline room degrades a little instead of
breaking.

## PDF

```
http://localhost:8081/index.html?print-pdf
```

then print to PDF (one page per slide — fragments are not split out). The
interactive panels export in their reset state, which is why every one of them
has a static slide nearby carrying the same point.
