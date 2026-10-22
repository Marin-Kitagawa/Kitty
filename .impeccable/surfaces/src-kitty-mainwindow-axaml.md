---
version: 2
slug: "src-kitty-mainwindow-axaml"
primary_target: "src/Kitty/MainWindow.axaml"
related_targets:
  - "src/Kitty/App.axaml"
  - "src/Kitty/MainWindow.axaml.cs"
---

# Surface brief — Kitty main window

Scope: the app's single main surface (browse trending by language). Mode: **Operate** — the user picks a language, scans a ranked list, filters, opens or copies. Brand lives in precise details; task and state always outrank expression.

Audience/job: the user themself, checking what's rising on GitHub daily, at a desk, keyboard- and glance-first.

Action/task: choose language (glass rail) → range (daily/weekly/monthly) → scan ranked rows → filter → open in browser / copy clone URL → manage source + token in settings.

Constraints: all controls native Avalonia Fluent (ListBox, TextBox, Buttons, RadioButtons); world enters only as type, palette, density, depth, and the signature moves; honest source labeling; keyboard focus + full contrast.

Memorable moments:
1. **The parallax stage** — holo grid, aurora blooms and opalescent orbs drift on their own and lean away from the cursor at different depths (code-behind pointer parallax), so the whole window feels like a lit glass case.
2. **The arrival stagger** — rows settle into the field in rank sequence on every fetch; hovering lifts a card slab with a rose halo, HUD corner brackets, a lit bevel seam and its language spine igniting.

Unresolved: none.

## Direction contract (v2 — "Noir Opaline")

THESIS: GitHub's rising repos presented inside a tiny opalescent atelier — an obsidian-plum night salon (dusk) or a porcelain rose salon (dawn) where every repository is a numbered slab with a gold rank medallion and a data-true language spine — refusing the category's default of a neutral card feed with a sidebar of filters.

OWN-WORLD: obsidian-plum ground (#110919 → #281639 dusk / #FDF7F2 → #F0E9F8 dawn), rose-orchid-champagne "rose-gold chrome" gradient (`PillBrush`) reserved for selection and primary actions, IBM Plex Mono letterspaced micro-caps as the instrument voice, Italiana hairline serif for display headings, Marcellus for section titles, Ballet script for the wordmark only, GreyQo for names/controls, Rochester/Ruthie signatures for warmth. GitHub linguist colors appear only as data (dots + card spines). 3D is built from depth: layered parallax planes, elevation shadows, bevel seam highlights, rotating halo rings — no fake screenshots or skeuomorphs.

STORY: "I stepped into my own little gallery; the pieces rising fastest in my language are lit on the wall, and the label tells me exactly where the numbers come from."

FIRST VIEWPORT: glass vitrine rail (THE COLLECTION + count, language search, language chips with data dots, purr footer) | main field headed by the language name in Italiana with a RISING NOW chip and the range capsule; filter field with result count; ranked card list; global console strip printing source, time and token state.

FINISH: this surface was rebuilt and visually reviewed in both worlds from live captures (dusk + dawn, see `.impeccable/review/final-*.png`); fonts Italiana and Marcellus are OFL downloads from the google/fonts repository, bundled in Assets/Fonts.
