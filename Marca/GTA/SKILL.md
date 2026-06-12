---
name: aliado-ti-design
description: Use this skill to generate well-branded interfaces and assets for Aliado TI (GTA — Global Technology Ally), either for production or throwaway prototypes/mocks/decks. Contains essential design guidelines, colors, type, fonts, assets, and UI kit components for prototyping.
user-invocable: true
---

Read the `readme.md` file within this skill, and explore the other available files.

Aliado TI is a warm, editorial, monochrome consultancy brand built on the "ally" (*aliado*) metaphor: cream paper, charcoal ink, a metallic-gray "A" mark, and a signature headline lockup that pairs a handwritten **Borel** script word with a heavy **Garet** word. Copy is Spanish-first, first-person plural, confident, no emoji.

Key files:
- `styles.css` — the single CSS entry point. Link it (or its `tokens/*`) to get every color/type/spacing token and `@font-face`.
- `readme.md` — full brand guide: Content fundamentals, Visual foundations, Iconography, and a file manifest.
- `guidelines/commercial-proposals.md` — the proposal playbook: how to build on-brand commercial proposals end to end.
- `assets/` — logo (`logos/aliadoti-mark.png`), fonts, and two editorial background photos.
- `components/` — React primitives (Button, Input, Select, Checkbox, Switch, Card, Badge, Tag, Avatar, Alert, Tabs, Stepper). Each has a `.d.ts`, a `.prompt.md`, and a `.card.html` demo.
- `ui_kits/portal-proveedores/` — a full self-contained product recreation (login → admin → wizard) and `kit.jsx` primitives you can copy.
- `slides/` — sample proposal deck slides at 1280×720.

If creating visual artifacts (slides, mocks, throwaway prototypes), copy assets out and create static HTML files for the user to view — link `styles.css`, use the tokens, and reach for the slide/UI-kit patterns. Use **Lucide** for icons; never emoji. If working on production code, copy assets and read the rules here to become an expert in designing with this brand.

If the user invokes this skill without other guidance, ask them what they want to build or design, ask a few focused questions, then act as an expert designer who outputs HTML artifacts _or_ production code, depending on the need.

Caveat: the body font **Mulish** is a substitution for an unshipped Garet body weight — flag this if the user has the licensed file.
