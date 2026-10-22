# Surface brief — Kitty main window

Scope: the app's single main surface (browse trending by language). Mode: **Operate** — the user picks a language, scans a ranked list, filters, opens or copies. Brand lives in precise details; task and state always outrank expression.

Audience/job: the user themself, checking what's rising on GitHub daily, at a desk, keyboard- and glance-first.

Action/task: choose language (drawer rail) → range (daily/weekly/monthly) → scan ranked rows → filter → open in browser / copy clone URL → manage source + token in settings.

Constraints: all controls native Avalonia Fluent (ListBox, TextBox, ComboBox-level semantics, Buttons, ToggleButtons); world enters only as type, palette, density, and the signature move; honest source labeling; keyboard focus + full contrast.

Memorable moment: the drawer-load stagger — rows laid into the drawer in rank sequence on each fetch; hover lifts a specimen card with a brass edge while its data label underlines.

Unresolved: none.

## Direction contract

THESIS: GitHub's rising repos presented as a naturalist's specimen drawer — every repo a numbered specimen with a typed data label — refusing the category's default of a neutral card feed with a sidebar of filters.

OWN-WORLD: deep drawer-brown shell (#221B15 rail/status, #2A2119 field), cream label ink #EDE4D3, brass #C7A24B for selection/accents, sage #7FA08C for period-gain; GitHub linguist colors as data dots. Playfair Display for the collection title only; small letterspaced caps for label fields; mono (IBM Plex Mono) strictly for numbers and codes; Segoe UI for UI text. Hairline dividers, catalog density.

STORY: "This drawer holds the 30 specimens rising fastest in my language, ranked; I can trust the label because the source is printed on the drawer."

FIRST VIEWPORT: left drawer-index rail (language tabs, selected tab extends with count); main column headed by the language name alone (no eyebrow), range segmented control + refresh + settings right-aligned; beneath, filter field with result count and honest source badge; the ranked specimen list fills the rest; status bar prints source, time, and warnings.

FORM: assigned direction "The Specimen Drawer", index 6 of the grounded list, seed key dc0ddbe5; raises: tabular mono figures (from ikeda-datamatics), coded language strips decoded as labeled dots (from saville-catalog-sleeve), fixed scale steps (from emigre-bitmap-specimen).

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance.
