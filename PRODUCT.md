# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

delegated: C# / .NET 10 + Avalonia 11 (Fluent theme) desktop app, per the user's explicit brief. GitHub REST API via HttpClient; no browser embedding.

## Users

The user themself: a developer who wants a quick, beautiful daily window onto what is rising across GitHub — in any programming language — instead of visiting github.com/trending in a browser.

## Product Purpose

Kitty browses GitHub trending repositories in all programming languages. The user picks any language, sees the trending repos for that language, filters within results, opens a repo in the browser, or copies its clone URL. Success = replacing the trending webpage with something faster and lovelier to check daily.

## Positioning

A native, language-first trending reader: pick language → trending list, with three time ranges, in-app filtering, and one-click clone URL — none of which the trending webpage offers together.

## Operating Context

- Desktop use (Windows primary), checked briefly and often; keyboard- and glance-friendly.
- Data plan (user-confirmed):
  - **Primary: GitHub Search API** approximating trending (`created:>N-days sort=stars` in the chosen language). Works unauthenticated at low rate; if the token-free quota fails, authentication becomes required.
  - **Optional sign-in via browser** (GitHub OAuth/device flow) purely to raise rate limits; never mandatory unless the token-free quota can't serve requests.
  - **Trending-page scraping is a fallback by default**; if the user explicitly turns the scraping setting on, it becomes the primary source (true trending ranking).
- Time ranges: daily, weekly, monthly.
- Design (settings toggle, added on request): the app ships **two designs** side by side.
  - **Classic** — the six visual worlds (Risograph Press · Character Shelf · Split-Flap Board · Cassette Insert · Language Programme · Category Standard), exactly as they were.
  - **Aurora Glass** — a redesign built from scratch, not a repaint: midnight-lilac ground, frosted translucent panes with lit edges, bubble-rounded lettering, deep soft shadows, floating capsule chrome and a bokeh backdrop in place of the holo grid.
  - The toggle lives at the top of settings; the world picker (and Ctrl+Shift+W cycling) applies to Classic. Choosing a classic world from the list returns to Classic with that world, and the last classic world is remembered.

## Capabilities and Constraints

- Language picker covering all languages GitHub trending knows (from GitHub's languages list).
- Repo row: name, owner, description, language (+ language color dot), stars, today/period stars where available, fork count.
- Actions on a repo: open in browser, copy clone URL.
- Text filter within loaded results.
- Settings: source mode (Search API + scrape fallback / scrape primary), account, cache.
- Sign in with GitHub **without a token**: OAuth device flow (RFC 8628) as a public client — the app shows a short code, opens github.com/login/device, and polls until the user approves. No client secret, no pasted token, no loopback listener. Needs a GitHub OAuth app with "Enable Device Flow" ticked; its client ID is entered in settings (or supplied via `KITTY_GITHUB_CLIENT_ID`). Device-flow tokens arrive with an expiry and a refresh token, so the app renews silently before a fetch and only asks for a new sign-in when GitHub refuses the credential.
- Pasted personal access tokens (`ghp_…`, `github_pat_…`) still work; a pasted token replaces any OAuth credential, and signing out clears the whole credential.
- Account state is derived from what is actually stored: the token row shows the dot + label "signed in" only while a token is saved and applied, otherwise "not signed in", and the sign-out action is offered only in the signed-in state — a typed-but-unsaved token does not count.
- Offline/rate-limited states must be honest and labeled, never faked data.

## Brand Commitments

Name: **Kitty** — continues the cute feminine Keyko/Lumen family identity. Visual identity (v2, user-delegated): the "Noir Opaline" atelier — obsidian-plum night glass with rose-gold chrome in dark mode, porcelain rose salon in light mode; 3D parallax depth stage; Italiana/Marcellus/IBM Plex Mono type system. No other binding visual commitments.

Second identity (user-delegated, on request): **Aurora Glass** — professional, glassy, cute, pretty. Midnight indigo-to-plum ground; frosted white panes at 7–13% with lit top edges; bubble-rounded Sniglet for every structural voice over IBM Plex Mono for the instrument voice; 22px panes on 999px pills; deep soft shadows and candy blooms (bubblegum, periwinkle, mint, butter). It replaces the classic chrome, header band and card anatomy rather than recolouring them, and it never borrows a classic world's palette or type stack.

## Evidence on Hand

None — greenfield. No fabricated metrics, avatars beyond what GitHub's API returns, or testimonials.

## Product Principles

1. Language-first: choosing a language is the core interaction, always one gesture away.
2. Honest data: label the source (API approximation vs scraped trending) and never show invented numbers.
3. Glanceable: the list is the product; everything else recedes.
4. Sign-in is a means (rate limits), never a gate.

## Accessibility & Inclusion

Standard contrast and full keyboard navigation; no color-only meaning (language dot always paired with the language name).
