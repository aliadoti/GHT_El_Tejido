# Aliado TI — Design System

**Aliado TI** (legally *Aliado de Tecnología*; English-facing name **GTA — Global Technology Ally**) is a technology partner that helps companies design, build, and evolve custom software, AI solutions, and digital systems. The brand voice is built entirely around one metaphor: **the ally** (*aliado*) — a strategic partner who orders and strengthens a client's process, not a vendor who ships features.

> "No proponemos únicamente desarrollar funcionalidades. Proponemos ordenar y fortalecer el proceso… trabajar como un *aliado estratégico* del cliente, no solo como proveedor de desarrollo."

This system encodes the brand as seen in Aliado TI's signature deliverable — a **client proposal deck** — and applies it to product UI. It is warm, editorial, monochrome and confident.

---

## Sources

Everything here was reverse-engineered from the assets in `uploads/`:

| Source | What it gave us |
|---|---|
| `uploads/plantillabasediseno.pdf` | The real, authoritative brand design — an 18-page Spanish client proposal ("Evolución del Portal de Proveedores" for *Gestiones y Representaciones Chía* / GHT). Layout system, type pairings, color mood, voice, footer pattern, photographic backgrounds, and the full functional spec behind the Portal UI kit. Rendered page imagery lives in `uploads/pdf_imgs/`. |
| `uploads/ati.png` | A **desaturated/grayscale** copy of the mark (misleading — see below). |
| `uploads/logo transparente 64.png` | The **real** logo — a blue→indigo gradient "A" → `assets/logos/brand-mark.png` (also kept as `aliadoti-mark.png`). **Shared by two distinct companies: Aliado TI (Aliado de Tecnología) and GTA (Global Technology Ally).** |
| `uploads/falcon.png` | A green/red tree mark belonging to a **client** (not Aliado TI). Not used in brand assets. |
| `uploads/Garet-Heavy.ttf` | Display headline face. |
| `uploads/PoiretOne-Regular.ttf` | Light geometric title face (also on Google Fonts). |
| `uploads/Borel-Regular.ttf` | Handwritten script accent (also on Google Fonts). |

No codebase, Figma, or live product was provided. The Portal UI kit is therefore a **brand-faithful interpretation** of a documented (but never visually-mocked) product — not a pixel copy of an existing UI.

---

## Content fundamentals — how Aliado TI writes

- **Language:** Spanish (Colombian, business register). The system ships bilingual-ready (ES/EN) because the product does, but brand copy is Spanish-first.
- **Voice / person:** First-person **plural** — *"Proponemos", "Entendemos", "Nuestro enfoque"*. The company is "nosotros / Aliado TI"; never "yo". The client is addressed by name and as a partner.
- **The ally frame:** Recurring vocabulary — *aliado estratégico, sostenible, ordenar y fortalecer, valor real, defendible desde lo comercial*. Solutions are "sostenibles, no parches temporales."
- **Tone:** Calm, structured, consultative. Confident without hype. Leads with the client's business problem, then resolves it. Risk, trazabilidad (traceability), and escalabilidad are virtues named explicitly.
- **Casing:** Sentence case in body. Section headlines use the signature **lockup**: a lowercase **script** phrase set against a **heavy** word — e.g. *"Lo que esta solución / **resuelve y habilita**"*, *"Cambios / **que generan valor**"*, *"Muchas / **gracias**"*. Eyebrows are UPPERCASE, wide-tracked (e.g. `RESUMEN EJECUTIVO`).
- **Numbers:** Capabilities and steps are numbered `01–06`. Concrete figures are stated plainly (440 horas, 5.5 sprints, 94% ANS).
- **Emoji:** **None** in brand copy. (Emoji appear only inside auto-generated technical diagrams in the source PDF — treat that as a diagram artifact, not brand.)
- **Footer:** Every page carries `Propiedad Aliado TI · Información Confidencial`.

---

## Visual foundations

**Overall feel:** warm editorial-luxury. Think gallery wall, soft daylight, generous negative space — the opposite of a saturated SaaS dashboard. Monochrome with one quiet metallic accent.

- **Color:** A warm-neutral ramp from cream paper (`--cream-100` page, `#FFFFFF` cards) down to charcoal ink (`--ink-900`). The brand's true hue is the **blue→indigo gradient** of the "A" mark (`--brand-cyan` → `--brand-blue` → `--brand-indigo`, plus `--gradient-brand`). There are **two accent registers**: warm **neutral metal** (`--metal*`, `--gradient-metal`) for the editorial / proposal-deck look the source PDF uses, and **logo blue** (`--brand-*`) for product UI and any deck that wants the brand color forward. Pick one per artifact. The **primary action color stays charcoal ink**. Status colors are deliberately muted. The client's green/red are *not* part of this palette.
- **Type:** Four voices — **Garet Heavy** (punchy display), **Poiret One** (airy light titles), **Borel** (one handwritten accent word per headline), **Mulish** (readable workhorse — *substituted*, see Caveats). Big type; display tracks tight (`-0.02em`).
- **Backgrounds:** Two registers. (1) Soft, warm **photographic** backgrounds on cover / closing slides — textured walls, still-life, lots of light — always pushed behind a cream gradient scrim so text stays legible. (2) Flat cream for content. No mesh gradients, no purple, no noise overlays beyond the photos' own grain.
- **Imagery vibe:** warm, bright, low-contrast, natural light; cream/beige with muted dusty-rose and sage incidentals. Never cold, never neon, never b&w.
- **Cards:** white surface, **1px sand hairline** (`--border-subtle`), **soft low warm shadow** (`--shadow-sm`), `--radius-lg` (16px) corners. Restraint is the rule — shadows are felt, not seen. Interactive cards lift `-2px` and deepen to `--shadow-lg`.
- **Corners:** soft but not pill-everything — 8–16px on most surfaces; full pills reserved for badges, switches, avatars.
- **Borders:** hairlines do the structural work (sand `#D8D2C4`); heavier strokes only on inputs/steppers (1.5px).
- **Elevation:** warm-tinted (`rgba(38,34,28,…)`), five steps, all low-opacity. No hard black drop shadows.
- **Motion:** calm and confident. `--ease-out` (no bounce, ever), 140–400ms. Hovers shift background a shade or lift a card; presses nudge `translateY(1px)`. Decorative looping animation is avoided.
- **Hover / press states:** primary buttons darken ink → `--ink-950`; secondary/ghost wash to `--cream-200`; rows tint to `--surface-raised`. Press = subtle downward nudge. Focus = a soft ink ring (`--focus-ring`), never a bright outline.
- **Transparency / blur:** used sparingly — translucent cream panels with `backdrop-filter: blur()` over photos (login card, image captions, deck nav). Not on flat content.
- **Layout:** roomy margins (64px on slides, 32px in app), the confidential footer pinned bottom, eyebrow → lockup → lead paragraph rhythm. Three-column capability grids; phased sprint tracks.

---

## Iconography

- **System:** **Lucide** (CDN: `unpkg.com/lucide@0.460.0`) — thin, geometric, consistent 2px stroke. It pairs naturally with the refined geometric type and the monochrome palette. This is a **chosen substitution**: the source PDF contained no icon set of its own (its diagrams used emoji, which we deliberately do **not** carry into the brand).
- **Color:** icons inherit text color — `--text-muted` at rest, `--text-primary`/`--cream-50` when active. Never multi-color.
- **Usage:** UI affordances (search, chevrons, nav, status, document actions). In `kit.jsx` a small `<Lucide n="…"/>` wrapper renders them; component cards load the UMD build and call `lucide.createIcons()`.
- **Emoji:** not used anywhere in the brand or product UI.
- **Logo:** `assets/logos/brand-mark.png` (alias `aliadoti-mark.png`) — the **blue→indigo gradient "A"**, shared by both companies (Aliado TI / GTA). Pair with the Garet wordmark; clear space ≈ cap height. The colored mark holds on cream and on `--ink-900` (don't invert it). See the Brand → Logo card.

---

## Index / manifest

**Root**
- `styles.css` — the single entry point consumers link. Import-only.
- `readme.md` — this guide. · `SKILL.md` — Agent-Skill wrapper.

**`tokens/`** (all `@import`ed by `styles.css`)
- `fonts.css` · `colors.css` · `typography.css` · `spacing.css` (spacing/radii/elevation/motion) · `base.css`

**`assets/`**
- `fonts/` Garet-Heavy, PoiretOne-Regular, Borel-Regular
- `logos/aliadoti-mark.png`
- `imagery/cream-wall-shadow.png`, `imagery/cream-desk-tea.png`

**`components/`** (React primitives — `window.AliadoTIDesignSystem_36ceeb`)
- `buttons/` — **Button**, **IconButton**
- `forms/` — **Input**, **Textarea**, **Select**, **Checkbox**, **Switch**
- `data-display/` — **Card**, **Badge**, **Tag**, **Avatar**
- `feedback/` — **Alert**
- `navigation/` — **Tabs**, **Stepper**
- Each directory has a `*.card.html` (Design System tab) + `.d.ts` props + a `.prompt.md`.

**`guidelines/cards/`** — foundation specimens for the Design System tab (Colors, Type, Spacing, Brand).

**`guidelines/commercial-proposals.md`** — **start here for proposals.** Complete brand + build guide for producing on-brand commercial proposals (setup, layout, type, color registers, voice, export, pre-send checklist).

**`ui_kits/portal-proveedores/`** — the Portal de Proveedores click-through (login → admin → wizard). See its `README.md`.

**`slides/`** — sample deck recreating the proposal: `index.html` (deck) + `TitleSlide`, `SectionSlide`, `CapabilitiesSlide`, `RoadmapSlide`, `ClosingSlide`.

---

## Caveats

- **Body font is substituted.** The uploads contain only display/script faces (Garet *Heavy* only, Poiret One, Borel). For readable body/UI text we use **Mulish** (Google Fonts) — the nearest neutral geometric-humanist match. **Please send licensed Garet Book/Regular/Medium/Bold** (or your preferred body face) to replace it; swap the `@import` in `tokens/fonts.css` and update `--font-body`.
- **Portal UI kit is interpretive.** No portal screenshots/code existed, so its visuals are derived from the brand system, not copied from a real UI.
- **Lucide icons are a choice,** not an extracted asset set (the source had none). Swap if you adopt a house icon set.
