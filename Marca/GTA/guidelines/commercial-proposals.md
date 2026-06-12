# Commercial Proposals — Brand & Build Guide

Everything you need to produce an **Aliado TI / GTA commercial proposal** that is on-brand, consistent, and indistinguishable from the reference deck (`uploads/plantillabasediseno.pdf` → *"Evolución del Portal de Proveedores"*).

This guide is self-contained for proposal work. For the full design system, see `readme.md`.

---

## 0. The 60-second brief

A proposal is a **warm, editorial, monochrome document** with one **script + heavy** headline per page, generous cream space, soft photographic covers, and a confidential footer carrying the **logo mark only** (no wordmark — works for both companies). Spanish, first-person plural, the *aliado* (ally) frame throughout. Color is used **sparingly and intentionally**: charcoal ink for primary emphasis, plus either the **warm neutral-metal** register or the **logo-blue** register — pick one per proposal, never both.

---

## 1. Setup — link the system

Every proposal page links the system stylesheet, then the slide foundation:

```html
<link rel="stylesheet" href="<path-to-system>/styles.css" />
<link rel="stylesheet" href="<path-to-system>/slides/slide.css" />
```

`styles.css` ships every token and the four fonts. `slide.css` provides the 1280×720 `.slide` frame, the `.slide__footer`, `.slide__eyebrow`, the `.lockup` (script+heavy), and `.slide__num`.

Start from the ready-made templates in `slides/` — copy and edit, don't rebuild:

| Template | Use for |
|---|---|
| `slides/TitleSlide.html` | Cover — photographic, client name, big lockup |
| `slides/SectionSlide.html` | Chapter break — giant index number on ink |
| `slides/CapabilitiesSlide.html` | Numbered `01–06` content grid (the workhorse) |
| `slides/RoadmapSlide.html` | Phased sprint roadmap (objetivo / actividades / entregables) |
| `slides/ClosingSlide.html` | Thank-you — photographic, contact block |
| `slides/index.html` | Stitches slides into a navigable, self-scaling deck (← →) |

---

## 2. Canvas & layout rules

- **Frame:** 1280×720 (16:9). Use the `.slide` class — never freestyle the size.
- **Margins:** 64px left/right on slides. Content starts ~84px from the left on cover/section slides.
- **Rhythm per page:** `eyebrow` (UPPERCASE, tracked) → `lockup` (script + heavy) → lead paragraph → content. Don't skip the eyebrow.
- **One idea per page.** If a slide has two headlines competing, split it.
- **Footer (mandatory, every page):** logo mark on the left, `Información Confidencial` on the right. The mark is the **logo only** — no "Aliado TI" / "GTA" wordmark next to it, so the same deck serves either company.

```html
<div class="slide__footer">
  <div class="slide__brand"><img src="<sys>/assets/logos/brand-mark.png" alt="Brand mark" /></div>
  <span>Información Confidencial</span>
</div>
```

---

## 3. Typography — the four voices

| Role | Font | Where |
|---|---|---|
| **Display / heavy** | Garet Heavy (`--font-display`, 900) | The loud word in every lockup, capability titles, big numbers |
| **Script accent** | Borel (`--font-script`) | **Exactly one** word/phrase per headline. Never body, never all-caps |
| **Title (light)** | Poiret One (`--font-title`) | Airy section titles, refined subheads, cover subtitles |
| **Body / UI** | Mulish (`--font-body`) *(substituted — see §8)* | All readable copy, lists, captions, tables |

**The signature lockup** — a lowercase script line stacked over a heavy word:

```html
<div class="lockup">
  <span class="script">Lo que esta solución</span>
  <span class="heavy">resuelve y habilita</span>
</div>
```

⚠️ **Spacing caution:** Borel has long descenders. Keep the script line's `line-height ≥ 1.15` and add `margin-bottom: ~12px` so it never overlaps the heavy word below (this bit the cover once). When in doubt, give the script line more room.

Sizes (cover): script ~50px, heavy ~84px. Content slides: script ~32px, heavy ~44px. Never set proposal type below ~13px.

---

## 4. Color — pick one register per proposal

The base is always **warm cream paper + charcoal ink**. Choose **one** accent register and stay with it for the whole document:

**A · Neutral metal** — the reference-deck look. Quiet, luxe, monochrome.
`--metal`, `--metal-dark`, `--gradient-metal`. Best for conservative / executive audiences.

**B · Logo blue** — brand-forward. Use the tones of the mark.
`--brand-cyan #11A0E2` · `--brand-blue #1378C6` · `--brand-blue-deep #155FAE` (text-safe on cream) · `--brand-indigo #2A3C9B` · `--gradient-brand`. Best when you want energy or to foreground the brand.

**Multi-hue phase set** (for roadmaps/timelines with distinct stages) — defined at the top of `RoadmapSlide.html` as `--phase-1 #1576C5` (blue) · `--phase-2 #1C8C86` (teal) · `--phase-3 #5A55B0` (violet). Swap these three values to re-theme an entire timeline in one place.

**Rules**
- **Primary emphasis stays charcoal ink** regardless of register.
- Accent color is for: numbers, eyebrows, bullets, step fills, small rules, the phase pill — **not** large fills behind text.
- Keep text on accent fills to `--cream-50`; use `--brand-blue-deep` (not cyan) for colored text on cream.
- **Status colors are muted** and reserved for genuine status (success/warning/danger) — don't decorate with them.
- The client's own colors (e.g. their logo green/red) never enter Aliado TI chrome.

---

## 5. Backgrounds & imagery

- **Cover & closing:** soft, warm, photographic. Use the brand mood images (`assets/imagery/cream-wall-shadow.png`, `assets/imagery/cream-desk-tea.png`) **or** the client's own warm still-life — always behind a cream gradient scrim so text stays legible:

```css
background:
  linear-gradient(90deg, rgba(251,250,246,.97) 0%, rgba(251,250,246,.9) 40%,
                  rgba(251,250,246,.3) 75%, rgba(251,250,246,.06) 100%),
  url(<image>);
background-size: cover; background-position: center;
```

- **Content pages:** flat cream (`--surface-page`). No photo.
- **Section dividers:** ink (`--surface-inverse`) with a giant translucent index number.
- **Image vibe:** warm, bright, natural light, low contrast. Never cold, neon, or black-and-white.
- **No** mesh gradients, no purple, no noise overlays.

---

## 6. Cards, dividers & detail

- **Cards:** white surface, 1px sand hairline (`--border-subtle`), soft low shadow (`--shadow-sm`), 16px radius (`--radius-lg`). Restraint — shadows are felt, not seen.
- **Capability items:** number in the accent color (`01`), heavy/bold title, one-line body. Three per row.
- **Roadmap columns:** colored top rule + dot per column (objetivo / actividades / entregables); bullet lists use accent dots.
- **Eyebrows:** `--text-muted`, 700 weight, UPPERCASE, `letter-spacing: .14–.22em`.
- **Corners:** 8–16px on surfaces; full pills only for badges/pills/avatars.

---

## 7. Voice & copy checklist

- **Spanish**, Colombian business register. (Bilingual ES/EN only if the engagement requires it.)
- **First-person plural** — *"Proponemos", "Entendemos", "Nuestro enfoque"*. Never "yo".
- **The ally frame** — *aliado estratégico, sostenible, ordenar y fortalecer, valor real*. Solutions are sustainable, "no parches temporales".
- **Lead with the client's problem**, then resolve it. Name trazabilidad, escalabilidad, control de riesgo as virtues.
- **Numbered** capabilities/steps (`01–06`). State concrete figures plainly (440 horas, 5.5 sprints, 94% ANS).
- **No emoji.** Anywhere.
- Address the client **by name**. Footer always `Información Confidencial`.

---

## 8. Production & export

- **Build:** copy a `slides/*.html` template, edit copy + imagery, keep the footer. Add pages by duplicating templates; register them in `slides/index.html`'s `slides[]` array to include them in the click-through deck.
- **Preview / present:** open `slides/index.html` — it self-scales to any screen and navigates with ← → arrows.
- **Export to PDF:** open the deck and use the Save-as-PDF flow (one page per slide, 1280×720 / 16:9).
- **Export to PowerPoint:** the slides are standard HTML — use the Export-as-PPTX flow if the client needs an editable file.

---

## 9. Pre-send checklist

- [ ] One **script + heavy** lockup per page, no overlap, script line has breathing room.
- [ ] **One** color register used consistently (metal **or** blue); primary emphasis is ink.
- [ ] Footer on **every** page: logo mark only + `Información Confidencial`.
- [ ] Cover & closing use a warm photographic background behind a cream scrim.
- [ ] Copy is Spanish, first-person plural, ally-framed, **no emoji**.
- [ ] Client named correctly; client's own colors kept out of Aliado TI chrome.
- [ ] No type below ~13px; numbers and figures concrete.
- [ ] Exported at 1280×720, one page per slide.

---

## Caveats

- **Body font is substituted.** Readable text uses **Mulish** (Google Fonts) as a stand-in for an unshipped Garet body weight. If you have licensed Garet Book/Regular/Medium/Bold, drop them in `assets/fonts/`, update the `@font-face` + `--font-body` in `tokens/fonts.css`, and proposals reflow automatically.
- **Logo resolution.** `assets/logos/brand-mark.png` is ~80×80px and softens above ~84px. For large cover marks, supply an SVG / higher-res export.
