---
summary: "Canonical spec for the OpenBrain v3.4 six-dimension thought-scoring rubric (SP/IE/AC/TR/CO/DU) and its propagation protocol."
created: 2026-05-15
updated: 2026-05-15
tags: [openbrain, rubric, spec, canonical]
related_notes: [PKM Workflow — Vault to Brain Loop.md, Getting Started — New Machine Setup.md, obsidian-supabase-brain-roadmap.md]
context_file: _claude-context.md
---

# OpenBrain Thought-Scoring Rubric — Canonical Spec

> **This document is the single source of truth for the OpenBrain rubric.** Every other file in the vault, every script, every skill, and every memory entry that references the rubric must either point here or be updated when this file changes. See the [Propagation Protocol](#propagation-protocol) at the end.
>
> **Current version:** v3.4 (effective 2026-05-27 — minimum-write threshold 0.60; tier recalibration; CO/DU scoring tightened; see §10 changelog)
> **Last reviewed:** 2026-05-27 (v3.4: write floor at 0.60 in §8.3; tiers recalibrated in §5; CO and DU scoring guidance tightened in §3.5/§3.6 to address observed inflation)
>
> **Forward-looking design** lives in §13 (Roadmap — thought → insight → meaning layered ontology). Not yet active.

---

## 1. Purpose

Every `[!brain]` callout in the vault carries a six-dimension score that drives two decisions:

1. **What gets pushed to OpenBrain.** `upload.py --min-score N` filters by composite score before pushing to Supabase. The threshold sets the quality bar.
2. **What deserves further curation.** Low-scoring thoughts in high-value notes are candidates for `/improve-thought`; high-scoring thoughts in marginal notes are evidence the note has been distilled well.

The rubric exists to make those decisions repeatable, defensible, and resistant to scope-bias (operational vs. strategic thoughts scored on the same scale).

---

## 2. Core Principle — Scope-Agnostic Scoring

The rubric is **scope-agnostic**. An operational, technical, or mechanistic insight (e.g., "the cold room behaves differently on Mondays after long weekends") is scored on the exact same scale as a strategic, structural, or organizational insight (e.g., "GHT's integrated network is the minimum structure required for the floral category").

**Do not reward grandiosity.** Reward specificity, inside-edge, actionability, transferability, completeness, and durability. A specific operational moat — e.g., "tutoraje de plantas de clave en QF" — can reach crystallized tier just as readily as a strategic governance thought. A grand-sounding strategic observation that any analyst could write scores low.

---

## 3. The Six Dimensions

| #   | Code | Dimension       | Weight |
| --- | ---- | --------------- | ------ |
| 1   | SP   | Specificity     | 0.25   |
| 2   | IE   | Inside Edge     | 0.20   |
| 3   | AC   | Actionability   | 0.20   |
| 4   | TR   | Transferability | 0.15   |
| 5   | CO   | Completeness    | 0.10   |
| 6   | DU   | Durability      | 0.10   |

### 3.1 SP — Specificity (weight 0.25, anchor dimension)

Precision and unambiguity of the claim. Concrete numbers, named entities, specific dates, measurable thresholds → high. Vague generalizations and hand-wavy statements → low.

The **anchor dimension** because it is the most objectively scoreable and the cheapest anti-bullshit check. If a thought is vague, no other dimension can rescue it.

**Score by example:**

| Score | Example                                                                                                                                          |
|-------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| 0.9   | "Costco's Pompano DC requires 18-stem premium bouquets with a 7-day post-harvest shelf life; stems failing at day 5 trigger full invoice chargebacks." |
| 0.5   | "Costco has strict quality and pack-size requirements for floral."                                                                               |
| 0.1   | "Retail quality requirements are important."                                                                                                     |

### 3.2 IE — Inside Edge (weight 0.20)

A **two-part test, both halves required for a score above the floor:**

- **(a) ORIGIN** — only knowable from inside GHT: internal operations, proprietary data, Felipe's direct experience, named customer relationships, internal mechanics. NOT something an external analyst could write from public sources.
- **(b) EDGE** — one of two forms:
  - **Competitive edge:** confers a structural external consequence — a moat, a hard-won discipline, a network position, or a capability that resists competitor replication.
  - **Execution edge:** enables or disciplines a specific internal decision or action that would materially differ without it — a threshold, a rule, a posture, a constraint.

ORIGIN is necessary but not sufficient. The two edge types determine the score band: competitive edge reaches the top band; execution edge reaches the middle band; neither edge reaches the lower bands.

**Diagnostic floor:** "Could this sentence appear in a public industry report or generic business book?" If yes → IE is low.

**Diagnostic ceiling (competitive edge):** "Could a competitor with capital and focus match this within 12 months?" If yes → IE is capped at the middle band at most.

**Diagnostic for execution edge:** "Would a GHT person act meaningfully differently in a specific situation if they didn't have this?" If yes → execution edge applies.

**Score by dual test:**

| Score range | Pattern |
|-------------|---------|
| 0.80–1.00 | Inside-only AND competitive edge — structural moat, hard-won discipline, or position rivals cannot replicate within 12 months |
| 0.50–0.75 | Inside-only AND execution edge — enables or disciplines a specific internal decision or action; no structural competitive moat |
| 0.25–0.50 | Inside-only, no meaningful edge — operational fact with no material consequence beyond documentation |
| 0.10–0.25 | Edge-flavored but not inside — any industry analyst could write this |
| 0.00–0.10 | Generic; neither origin nor edge |

Named GHT entities (farms, accounts, internal programs) move ORIGIN up but do not by themselves move EDGE up.

**Speculation guardrail.** When an author claims operational authority in a domain where they have no first-hand vantage, **IE is capped at 0.30** regardless of how confident the prose sounds. This prevents inflating IE on strategic-sounding observations about, e.g., farm-floor mechanics the author has no direct involvement in. The cap is binding even if every other element of IE looks strong.

IE absorbs the dimensions formerly known as Competitive Advantage (v2 CA) and Institutional Specificity (v2 IS / v1 IS) into a single orthogonal axis. See §7 for migration rules.

### 3.3 AC — Actionability (weight 0.20)

Does the thought enable a **specific** decision, predict an outcome, or explain a causal mechanism that changes how someone acts?

- **High** = "because of this, we should/will/can [specific action or anticipation]."
- **Low** = pure observation with no behavioral consequence.

A precise, true claim that doesn't shift any decision scores moderate at best; the same claim with operational consequence ("...therefore allocate X volume to Y account in Q4") scores high.

**Score by behavioral consequence:**

| Score range | Pattern                                                                                                                  |
|-------------|--------------------------------------------------------------------------------------------------------------------------|
| 0.9–1.0     | Directly drives a specific decision, allocation, or predicted outcome that someone would act on differently if they didn't know it |
| 0.6–0.8     | Reveals a causal mechanism that constrains or guides future decisions in a clear class of situations                     |
| 0.3–0.5     | Changes how someone thinks about a topic but doesn't shift any specific decision                                         |
| 0.0–0.2     | Pure observation, descriptive without consequence                                                                        |

### 3.4 TR — Transferability (weight 0.15)

Does the insight describe a **structural** pattern that holds beyond the specific event or note that generated it?

- **High** = generalizable rule, repeatable dynamic, or causal mechanism that should reliably apply elsewhere.
- **Low** = one-off data point tied to a single situation.

**Test:** "Is there a structural rule or repeatable dynamic here that would hold in other contexts or future situations?" High = yes, clearly. Low = no, this is bound to a single event/date/buyer.

### 3.5 CO — Completeness (weight 0.10)

Is the thought fully self-contained? Could a reader with GHT domain context understand and **act on** it **without** access to the source note?

CO is closer to a **prerequisite than a co-equal axis** — if a thought is not standalone, the other scores are fictional. Weighted low because well-extracted thoughts should pass this gate by construction — but this means CO should **discriminate harshly**, not hand out 0.80+ by default.

**Scoring discipline:** CO has historically inflated (median 0.82 across the vault). The root cause is treating "grammatically standalone" as sufficient. It is not. CO measures whether the thought carries enough context to be **actionable in isolation** — not merely readable.

**Score by standalone actionability:**

| Score range | Pattern |
|-------------|---------|
| 0.85–1.00 | Fully self-contained: every entity named, every causal claim grounded, every number contextualized. A reader could make a decision based on this thought alone. |
| 0.65–0.85 | Readable in isolation but missing one element needed to act — e.g., a threshold without units, a comparison without the baseline, or a named entity whose role is assumed. |
| 0.40–0.65 | Grammatically standalone but operationally incomplete — the reader understands the claim but cannot evaluate or apply it without retrieving the source context. |
| 0.00–0.40 | Depends on surrounding text. Uses pronouns without antecedents, references "the above," or presupposes structure from the source note. |

**Anti-pattern:** A thought that says "this is important for GHT's competitive position" without specifying *what* is important or *how* it affects competitive position is CO ≤ 0.50 regardless of how well-formed the sentence is.

### 3.6 DU — Durability (weight 0.10)

How robust is the **insight** to plausible context shifts?

- **High** = structural mechanism that survives changes — new buyer, new regulatory regime, new season, new supply configuration.
- **Low** = bound to one moment/buyer/regime with no underlying mechanism.

**Critical discipline:** DU is about the **insight's** structural robustness, **not the situation it describes**. A thought about a fragile customer relationship can be high-DU if the pattern it captures (the dynamic that creates the fragility) is itself durable. Conversely, a precise observation that only holds for one quarter or one buyer with no transferable mechanism is low-DU even if the observation itself is sharp.

**Scoring discipline:** DU has historically inflated (median 0.65 across the vault). The root cause is treating any observation about a recurring topic (pricing, logistics, retail) as structurally durable. It is not. DU measures whether the **mechanism or pattern** described would survive a material change in the conditions that produced it — not whether the *topic* will remain relevant.

**Anti-pattern:** "GHT's cold chain is important for quality" is DU ≤ 0.40 — the topic (cold chain) is durable but the insight (it's important) carries no mechanism that could break or hold under changed conditions. Compare: "GHT's cold chain advantage erodes when transit exceeds 72 hours because [specific mechanism]" — here the mechanism is testable and durable.

**Score by insight robustness:**

| Score range | Pattern                                                                                                                          |
|-------------|----------------------------------------------------------------------------------------------------------------------------------|
| 0.85–1.00   | The pattern or mechanism is structural and survives even major regime changes (new regulation, market restructuring, technology shift). You would teach this to a successor. |
| 0.65–0.85   | Pattern likely to hold for 3+ years across plausible market shifts, but depends on conditions (e.g., current channel structure, current regulatory environment) that could evolve. |
| 0.40–0.65   | Pattern depends on conditions that may not persist (one regime, one customer, one supply configuration) but the dynamic is recognizable and may recur. |
| 0.20–0.40   | Observation is about a durable topic but the specific claim is time-bound or condition-bound with no transferable mechanism. |
| 0.00–0.20   | Insight is bound to a single moment in time with no transferable mechanism — true now, irrelevant in 18 months.           |

---

## 4. KIND — Categorical Classification (non-weighted)

Every `[!brain]` callout written under v3.2 carries a `kind:` classification that describes the **epistemic status** of the claim — what kind of thing the thought is, separate from how well-scored it is. KIND is **categorical, not numerical**: a well-distilled aspiration and a well-verified fact can have identical composite scores yet warrant completely different retrieval and decision treatment.

KIND does NOT enter the composite formula in §5. It is a sibling attribute to the six weighted dimensions, recorded on the marker line and surfaced at retrieval time so a reader (human or agent) can filter by claim type: "show me only verified facts about Costco" vs. "show me current hypotheses about wetpack" vs. "show me 2027 aspirations across the network."

### 4.1 The Five Kinds

| Kind         | Definition                                                                                                                | Diagnostic question                                                                |
|--------------|---------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------|
| `fact`       | An observed, verifiable state of the world — current or past. Evidence-backed; not in dispute within GHT.                  | "Is this true now / was this true then, and could I show the receipts?"            |
| `hypothesis` | A plausible inference under development. May be supported by evidence but is not yet confirmed; subject to refutation.    | "Am I proposing this as likely true, knowing it could turn out wrong?"             |
| `principle`  | A normative commitment — "how we operate" or "how we ought to operate." An ongoing rule or posture, not a one-time choice. | "Is this a rule or posture we hold, not a fact about the world?"                   |
| `aspiration` | A goal, target, or intended future state. Not yet true; carries normative wanting.                                         | "Is this something we want to be true, not something that is true?"                |
| `question`   | An explicit open inquiry — registered ignorance worth tracking. Distinct from hypothesis because no answer is proposed.    | "Am I stating what I don't know, rather than what I think?"                        |

### 4.2 Boundary Calls

- **`fact` vs. `hypothesis`** — if the source presents an observation the author has the vantage to make, → `fact`. If the source presents a *cause* or *mechanism* that isn't yet confirmed, → `hypothesis`.
- **`principle` vs. `fact`** — if the claim describes how the world *is*, → `fact`. If it describes how GHT *acts* or *commits*, → `principle`. ("GHT serves Costco DSD" = fact; "GHT protects anchor accounts during shortage" = principle.)
- **`principle` vs. `aspiration`** — principles are in effect now; aspirations are not yet realized. ("We provide emergency volume to anchors" = principle; "We will lead DSD logistics by 2027" = aspiration.)
- **`hypothesis` vs. `question`** — a hypothesis proposes an answer ("X is probably caused by Y"); a question registers ignorance ("what causes X?").
- **anti-pattern / failure mode** → `principle` (inverted: a commitment to avoid).
- **lesson learned / risk** → `fact` with the temporal or evidential context carried in the body.
- **definition / vocabulary** → not a thought; do not extract as a callout. Definitions belong in reference notes or glossaries, not the brain.

### 4.3 Classification Responsibility

`curate.py` and `rescore.py` classify KIND as part of the extraction/re-score pass. The model returns `kind:` alongside the six dimension scores in the JSON output. `/improve-thought` re-classifies KIND when a callout is upgraded. Manual edits via Cortex must include a `kind:` value.

When KIND cannot be determined from the source — typically for legacy v3 / v3.1 callouts written before v3.2 — the marker carries `kind:—` (unset) until a re-curation or `/improve-thought` pass classifies it. `upload.py` accepts `kind:—` and uploads anyway (the composite score and tier are unaffected), but the brain entry will lack the retrieval filter.

### 4.4 Expansion Path (Documented, Not Active)

If the five-kind set proves too coarse at retrieval scale, two additional kinds may be split out without re-classifying existing callouts:

- `decision` — a specific historical commitment ("GHT exited wholesale in 2024"). Currently folded into `principle`; promote if specific binding acts need separation from ongoing rules.
- `prediction` — a forward-looking claim with truth value testable by waiting ("Costco DSD exceeds wholesale by Q4 2026"). Currently folded into `hypothesis`; promote if forecasts need separation from explanatory hypotheses.

These belong to the documented seven-kind set; promote only when retrieval pain justifies the higher classification error rate.

---

## 5. Score Formula and Tiers

**Composite score:**

```
score = SP × 0.25 + IE × 0.20 + AC × 0.20 + TR × 0.15 + CO × 0.10 + DU × 0.10
```

Score each dimension independently. Use exact decimal values; do not round individual scores before computing the composite. The composite is rounded to 2 decimal places for display.

**Tier thresholds (v3.4, effective 2026-05-27):**

| Tier         | Range       | Symbol | Meaning |
|--------------|-------------|--------|---------|
| Crystallized | ≥ 0.80      | ✦      | Fully distilled, brain-ready. Push on next upload. |
| Developing   | 0.70 – 0.79 | ◈      | Strong but has room to sharpen. Candidate for `/improve-thought`. |
| Seed         | 0.60 – 0.69 | ·      | Written but needs work. Worth keeping, not yet worth pushing. |
| *(not written)* | < 0.60   | —      | Below minimum-write threshold (§8.3). Silently discarded during extraction. |

**Recalibration note (2026-05-27):** The original 0.50/0.80 boundaries (v2–v3.3) were set before any distribution data existed. After the first full sweep produced ~1,800 callouts with 63% scoring below 0.60, the write floor was raised to 0.60 and the tier boundaries recalibrated to distribute the written range meaningfully. The old Seed tier (< 0.50) and lower Developing band (0.50–0.59) are now below the write threshold — thoughts in that range are not extracted at all.

---

## 6. Marker Line Format

Every `[!brain]` callout written under v3.3 ends with a **multi-line marker block**. Each marker line is a callout-continuation (`> `) line; the first marker line carries a `^` sentinel so parsers can locate the start of the block. The full callout looks like:

```
> [!brain] Title here
> Body text — 1 to 3 sentences, fully self-contained.
> ^date_created:YYYY-MM-DD
> date_uploaded:YYYY-MM-DD|—
> kind:KIND
> SP:X.XX IE:X.XX AC:X.XX TR:X.XX CO:X.XX DU:X.XX | score:X.XX
> tier:TIER
```

**Field definitions** (one field per marker line, in this exact order):

| Line | Field                | Meaning                                                                                                                                       |
|------|----------------------|-----------------------------------------------------------------------------------------------------------------------------------------------|
| 1    | `^date_created`      | Date the thought was extracted/captured into the source note. Set by `curate.py` at run time; set manually for hand-captured thoughts. The `^` sentinel marks the start of the marker block. Legacy form: `staged:` (renamed 2026-05-14). |
| 2    | `date_uploaded`      | Date the thought was pushed to OpenBrain via `capture_thought` / `upload.py`. Use `—` until upload has actually succeeded. Legacy form: `pushed:` (renamed 2026-05-14). |
| 3    | `kind`               | Categorical classification (§4): one of `fact`, `hypothesis`, `principle`, `aspiration`, `question`, or `—` (unclassified — legacy v3/v3.1 callouts awaiting re-touch). Lowercase. NOT included in the composite score. |
| 4    | `SP IE AC TR CO DU \| score` | Per-dimension scores (two decimals, in weight order — SP highest) followed by the weighted composite. Pipe separator between dimensions and composite. |
| 5    | `tier`               | One of `seed`, `developing`, `crystallized`. Canonical lowercase. Other values (e.g., `ready`) are rejected by the pre-upload checks.        |

**Block boundaries.** The marker block begins at the first line whose content begins with `^date_created:` (or a legacy sentinel — `^staged:`, `^pushed:`, `^score:`) inside the callout, and ends at the callout's last `> ` line. Parsers must read multi-line blocks, not assume single-line. Single-line legacy markers (v3.0–v3.2) remain parseable for backward compatibility (see §7.1).

**Never write `date_uploaded:YYYY-MM-DD` until the `capture_thought` / `upload.py` call has actually succeeded.** Capture (into the note) and upload (to the brain) are separate steps; conflating them produces phantom-uploaded records.

---

## 7. Legacy Formats and Migration Rules

Four legacy formats coexist with v3.2. All remain parseable by `curate.py` and `upload.py` (which extract `score` and `tier` from the marker line regardless of which dimension fields are present). **No automatic batch migration is performed**; legacy callouts are upgraded one-at-a-time via `/improve-thought` or `--force` re-curation.

| Version            | Effective              | Dimensions         | Weights                                 | KIND field | Marker format |
|--------------------|------------------------|--------------------|-----------------------------------------|------------|---------------|
| **v3.3 (current)** | 2026-05-16 →            | SP IE AC TR CO DU  | 0.25 / 0.20 / 0.20 / 0.15 / 0.10 / 0.10  | required   | multi-line block (§6) |
| v3.2 (legacy)      | 2026-05-16 (morning)   | SP IE AC TR CO DU  | 0.25 / 0.20 / 0.20 / 0.15 / 0.10 / 0.10  | required   | single-line (pipe-separated) |
| v3.1 / v3 (legacy) | 2026-05-15 afternoon → 2026-05-16 | SP IE AC TR CO DU | 0.25 / 0.20 / 0.20 / 0.15 / 0.10 / 0.10 | absent (parses as `kind:—`) | single-line (pipe-separated) |
| v2 (legacy)        | 2026-05-15 morning      | CA SP UN TR CO IS  | 0.25 / 0.20 / 0.20 / 0.15 / 0.10 / 0.10  | absent     | single-line |
| v1 (legacy)        | pre-2026-05-15          | SP TR CO NV IS AC  | 0.25 / 0.20 / 0.20 / 0.15 / 0.15 / 0.05  | absent     | single-line |
| bare-score         | Session 1–5             | `^score:X.XX \| tier:TIER` only | —                            | absent     | single-line  |

### 7.1 Migration from v3.2 (single-line marker, `kind:` present)

- All six dimension scores, composite, tier, and `kind:` carry over directly. No re-score required.
- **Rendering only:** rewrite the single-line marker as the v3.3 multi-line block in §6, preserving every value.
- Triggered on next touch (re-curation, `/improve-thought`, or manual edit). No batch sweep is required; single-line v3.2 markers remain parseable.

### 7.2 Migration from v3 / v3.1 (SP / IE / AC / TR / CO / DU, no `kind:`)

- All six dimension scores carry over directly — no re-score required unless `/improve-thought` is invoked.
- **`kind:` is classified fresh** on next touch using the §4.1 definitions and §4.2 boundary calls.
- **Rendering:** rewrite as the v3.3 multi-line block (§6). Until re-touched, the v3.1/v3 single-line form with no `kind:` parses as `kind:—`.

### 7.3 Migration from v2 (CA / SP / UN / TR / CO / IS)

- **SP, TR, CO** → carry over directly (re-score optional).
- **CA + IS** → combine into a freshly re-scored **IE**. Both halves of IE (origin + edge) are present across these two dimensions, but **the IE re-score is not a relabel** — evaluate freshly against the v3 definition. CA contributes the edge signal; IS contributes the origin signal; both must be re-tested.
- **UN** → retired. The relevance/different-content content folds into IE.
- **AC, DU** → freshly scored (new in v3 — no v2 equivalents).
- **`kind:`** → classified fresh (§4.1).

### 7.4 Migration from v1 (SP / TR / CO / NV / IS / AC)

- **SP, TR, CO** → carry over directly (re-score optional).
- **AC** → carries directly to v3 AC (re-score optional).
- **IS + NV** → feed into freshly-scored **IE**. IS gives origin signal; NV gives partial different-content signal — the edge half must be evaluated freshly.
- **DU** → freshly scored (new in v3).
- **`kind:`** → classified fresh (§4.1).

### 7.5 Migration from bare-score legacy

All six v3 dimensions are scored fresh, and `kind:` is classified fresh (§4.1). Use today's date as `date_created:` if no original date is recoverable from the surrounding context.

### 7.6 Non-negotiables when migrating

- **The IE re-score is never a relabel.** Both halves (origin + edge) must be evaluated freshly against the v3 definition, even when v2 CA/IS or v1 IS/NV scores are available as inputs.
- **Never back-fit dimension scores from a known composite.** Re-score through the rubric.
- **Preserve `date_created`.** Use the pre-rename `staged:` date if upgrading from that form. Never overwrite with today's date when an original date exists.
- **Never touch `date_uploaded`.** That field is exclusively managed by `upload.py`. Migration is a local edit — it does not re-upload.
- **Tier must use canonical values:** `seed`, `developing`, `crystallized`. Reject custom tiers like `ready`.
- **`kind:` must use canonical values:** `fact`, `hypothesis`, `principle`, `aspiration`, `question`, or `—` (only for unclassified legacy). Reject anything outside this set, including the expansion-path values `decision` and `prediction` (see §4.4 — not yet active).

---

## 8. Extraction Discipline

Beyond scoring, the rubric implies extraction rules — what counts as a thought worth scoring at all.

### 8.1 Do not extract

- Action items or open tasks ("Need to call the buyer by Friday")
- Raw data points without interpretation ("Revenue was $2.3M in March")
- Section headings or labels that lack body content
- Statements whose meaning depends on surrounding text

### 8.2 Granularity

One callout per atomic claim. If a sentence contains two separable insights, split them into two callouts. Do not bundle. Two insights that share the *same* KIND but differ in content are still two callouts; one insight straddling two kinds (rare) should be split along the KIND boundary.

### 8.3 Minimum-Write Threshold

**Do not write a `[!brain]` callout unless the composite score ≥ 0.60.** Thoughts scoring below 0.60 are silently discarded during curation — they do not merit callout space in the source note. This applies to `curate.py`, `rescore.py`, `/improve-thought`, and any manual extraction.

**Rationale:** The vault's first full curation sweep (v1, 2026-05-02) produced ~1,800 callouts, 63% of which scored below 0.60. The volume-to-signal ratio was too low — the majority of extracted thoughts were vague restatements, context-dependent fragments, or observations any industry analyst could write. A 0.60 floor retains approximately the top third by quality and eliminates noise that would otherwise require manual `/improve-thought` passes to fix or manual deletion to clear.

**Exception:** `/improve-thought` may operate on an existing callout that currently scores below 0.60 (e.g., a legacy callout being upgraded). If the improved score still falls below 0.60 after the improvement pass, the callout should be deleted rather than left in place.

### 8.4 Edge cases

- **Note contains only tasks, dates, or logistics** → no callouts; the note has no extractable insights.
- **Claim is partially self-contained** → score CO low, but do not omit the insight. Surface it and let CO reflect the gap.
- **Ongoing situation with uncertain outcome** → score IE/DU on the structural pattern or mechanism, not the outcome itself. The insight is in the dynamic, not in how the situation resolves.
- **Contradictory signals in the same note** → extract each as a separate insight. Do not average or suppress either.
- **Operational vs. strategic thoughts** → scored on the same rubric. Neither is intrinsically "better." A specific operational moat can reach crystallized just as readily as a strategic one. See §2.

---

## 9. Composite Body Rule

When a thought is **about** a composite/scorecard/weighted-aggregate metric, the body text must enumerate the underlying dimensions and weights inline — not merely refer to "the composite."

Example: a thought about the Customer Health scorecard must list all five dimensions (absolute margin contribution, per-FEB profitability, seasonal alignment, portfolio alignment, adjusted third-party share) and their weighting.

**Reason:** composite scores are opaque on retrieval without their components. A reader pulling this thought from the brain without the source note has no way to reconstruct what the composite measures.

A related rendering rule: anywhere a composite score is shown in Obsidian (under `## OpenBrain Thoughts` callouts, summary tables, exports), the **per-dimension breakdown must appear inline alongside it**. Bare composites must not be surfaced.

---

## 10. Version History and Rationale

### v3.4 (2026-05-27 — write floor, tier recalibration, CO/DU tightening)

**Trigger:** The first full curation sweep (v1, 2026-05-02, ~1,800 callouts) revealed that 63% of extracted thoughts scored below 0.60 and 31% below 0.50. The vault was accumulating noise faster than signal. Two root causes: (1) no minimum quality bar for writing a callout — every extractable claim became a callout regardless of score; (2) CO and DU dimensions were inflating composites — CO had a vault-wide median of 0.82 and DU a median of 0.65, both significantly above the discriminating dimensions (SP 0.53, IE 0.47, AC 0.53).

**Changes:**

1. **Minimum-write threshold (§8.3).** Thoughts scoring below 0.60 composite are not written as `[!brain]` callouts. They are silently discarded during extraction. This eliminates the bottom ~63% of the prior distribution.
2. **Tier recalibration (§5).** With the write floor at 0.60, the old Seed (< 0.50) and lower Developing (0.50–0.59) bands are below the write threshold. New tiers: Seed 0.60–0.69, Developing 0.70–0.79, Crystallized ≥ 0.80.
3. **CO scoring tightened (§3.5).** Added scoring bands that distinguish "grammatically standalone" (insufficient — CO ≤ 0.65) from "actionable in isolation" (the real bar). Added explicit anti-pattern. Documented the observed inflation and its root cause.
4. **DU scoring tightened (§3.6).** Added a five-band scoring table (was four bands). Distinguished "durable topic" (insufficient) from "durable mechanism" (the real bar). Added explicit anti-pattern. Documented the observed inflation.

No change to dimension labels, weights, composite formula, marker format, or KIND set.

### v3.3 (2026-05-16 — multi-line marker block)

**Trigger:** Single-line pipe-separated marker lines were illegible at retrieval and during manual review. With KIND added in v3.2, the marker line carried 10+ fields and exceeded a typical terminal width; readers could not scan structure at a glance.

**Change:** The marker line in §6 is now a **multi-line block** with one logical field per line: `^date_created`, `date_uploaded`, `kind`, dimension scores + composite, `tier`. The `^` sentinel remains on the first marker line (date_created) so parsers can locate the start of the block. No change to field names, values, dimensions, weights, formula, tier thresholds, or the KIND set — pure rendering change.

Single-line v3/v3.1/v3.2 markers remain parseable for backward compatibility (see §7.1). Re-rendering to multi-line happens on next touch (re-curation, `/improve-thought`, or manual edit).

### v3.2 (2026-05-16 — KIND categorical classification)

**Trigger:** The rubric measured *quality* (specificity, edge, actionability, etc.) but not *kind* — there was no way to distinguish a verified fact about Costco from a current hypothesis about Costco from a 2027 aspiration about Costco. All three could score identically on the composite yet warrant completely different retrieval and decision treatment.

**Change:** Introduced a non-weighted categorical attribute `kind:` with five values — `fact`, `hypothesis`, `principle`, `aspiration`, `question` (§4). The classification is orthogonal to the six weighted dimensions, recorded on the marker line (§6), and applied by `curate.py` / `rescore.py` / `/improve-thought` during extraction or re-score. Pre-v3.2 callouts parse as `kind:—` until re-touched.

No change to dimension labels, weights, composite formula, or tier thresholds. The expansion path to `decision` and `prediction` (seven-kind set) is documented in §4.4 but not active.

### v3.1 (2026-05-16 — IE band refinement)

**Trigger:** IE was collapsing two distinct edge types into one band, causing inside-only operational/tactical knowledge to score either too high (confused with competitive moat) or too low (penalized as "no edge" despite material execution consequence).

**Change:** Split the EDGE half of IE into two types — *competitive edge* (structural moat, external, hard to replicate) and *execution edge* (enables or disciplines a specific internal decision or action). Added a dedicated 0.50–0.75 band for execution-edge thoughts. Top band (0.80–1.00) now reserved for competitive edge only. Added a third diagnostic question: "Would a GHT person act meaningfully differently in a specific situation if they didn't have this?"

No change to dimension labels, weights, or composite formula.

### v3 (2026-05-15 afternoon → v3.1)

**Trigger:** Felipe identified that v2 had three correlated dimensions (CA, UN, IS) all measuring "is this a GHT moat?" from different angles — 45% of total weight on collinear signals. The rubric was double- and triple-counting moat-flavor at the expense of orthogonal signals like actionability and durability.

**Changes:**
- Collapsed CA + UN + IS into a single orthogonal axis: **IE (Inside Edge)** with its two-part origin-AND-edge test.
- Restored **AC (Actionability)** at meaningful weight (0.20). v2 had absorbed AC's signal into other dimensions; v1 had it at only 0.05. v3 weights it at 0.20 — equal to IE.
- Added **DU (Durability)** — new in v3. Surfaces fragile insights that v2 was missing.
- Kept SP at 0.25 (anchor), TR at 0.15, CO at 0.10.

### v2 (2026-05-15 morning, legacy)

CA SP UN TR CO IS with weights 0.25/0.20/0.20/0.15/0.10/0.10. Retired the same day after collinearity diagnosis.

### v1 (pre-2026-05-15, legacy)

SP TR CO NV IS AC with weights 0.25/0.20/0.20/0.15/0.15/0.05. The original 6-dimension rubric used during the initial curation sweep (2026-05-02, 1,479 callouts written).

### Bare-score (Session 1–5, pre-`curate.py`)

`^score:X.XX | tier:TIER` only. No per-dimension breakdown. From the manual curation sessions before `curate.py` was built.

---

## 11. Propagation Protocol

**This document is the single source of truth for the OpenBrain rubric.** When this document changes, the changes must be propagated by hand to the locations below. There is no automated propagation. CI does not exist for this vault — the discipline is manual.

### 11.1 Edit checklist

When editing this document, in order:

1. Update the **Last reviewed** date in the header banner.
2. If changing a dimension, weight, or formula: bump the major version. v3 → v4. Update §10 with the rationale and date. Add the prior version to §7 (legacy formats) with a migration rule subsection (§7.x).
3. If changing the marker line format (including adding non-weighted fields like `kind:`): update §6 *and* update the regex / parser logic in `upload.py` (`SECTION_RE`, score/tier extraction, KIND field parsing).
4. If changing tier thresholds: update §5 *and* `curate.py:build_callout` (the inline `>= 0.80 / >= 0.50` branches).
5. If changing the KIND set (adding/removing/renaming a `kind:` value): update §4.1 *and* the classification logic in `curate.py:SYSTEM_PROMPT` and `rescore.py:SYSTEM_PROMPT`, *and* the validator list in `upload.py` (canonical-kind check).
6. Walk the propagation table below and update each mirror.

### 11.2 Propagation targets

| File                                                                                   | What lives there                                                                                  | Action on rubric change                                                                                                                                                |
|----------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `curate.py` (`SYSTEM_PROMPT` constant + `build_callout` function)                      | The operational copy — what the Anthropic API actually receives, plus the marker-writing logic.   | **Mirror this doc.** Edit the SYSTEM_PROMPT prose to match §2–§4, §8. Update `build_callout` weights to match §5; emit the v3.3 **multi-line marker block** on write (`^date_created` / `date_uploaded` / `kind` / scores+composite / `tier` — one per `>` line). Add KIND classification to the output JSON schema. Update the top-of-file docstring table. |
| `rescore.py` (`SYSTEM_PROMPT` constant + marker writer)                                | Second operational copy — what the Anthropic API receives during in-place re-scoring of existing callouts. | **Mirror this doc.** Edit the SYSTEM_PROMPT prose to match §2–§4 (esp. §3.2 IE bands, §3.6 DU discipline, §4 KIND definitions and boundary calls). Update the marker writer to emit the v3.3 multi-line block, and the marker parser to accept BOTH single-line legacy markers (v3.0–v3.2) and multi-line markers (v3.3+). Round-trip `kind:` through re-scoring; only overwrite if a fresh classification is more confident. |
| `upload.py` (docstring + parser)                                                       | Parser-side reference to all parseable formats and validator.                                     | Update the format-history docstring (v3.2 added `kind:`; v3.3 added multi-line block). Extend `CALLOUT_RE` / parser to accept multi-line marker blocks AND single-line legacy markers — read everything from the marker block boundary (first `^date_created` / `^staged` / `^pushed` / `^score` line) to the end of the callout. Extract `kind:` and validate against the canonical set (§4.1). Treat `kind:—` as parseable but flag at upload time. The `stamp_callout()` write path must update `date_uploaded:` on whichever line it lives — single-line for legacy, dedicated line for v3.3. |
| `.claude/plugins/ght-vault/skills/improve-thought/SKILL.md`                            | The `/improve-thought` skill flow. Carries a mini-table of dimensions/weights for bar-rendering. | Update the mini-table (Step 4) if weights change. Add a Step 6b classification check: when re-scoring, also re-classify or confirm `kind:`. Do NOT restate dimension or KIND definitions — the skill points here for those. |
| `_claude-context.md` (vault root)                                                      | Default Claude context. Carries the marker format example and a one-line summary per dimension.   | Update the one-line dimension summaries if a label or weight changes. Update the marker format example if §6 changes. Add a one-line summary of KIND with the five canonical values. |
| `Work Vault/03. Resources/OpenBrain Setup/PKM Workflow — Vault to Brain Loop.md`       | Workflow doc — pre-upload rules, post-session checklist, marker-fields one-liners.                | Update marker-fields one-liners only if §6 changes (v3.2 adds `kind:`). Add a KIND row to the marker-fields table. Pre-upload rules and post-session checklist are workflow rules, independent of rubric content. |
| `Work Vault/03. Resources/OpenBrain Setup/obsidian-supabase-brain-roadmap.md`          | Historical design record — Sessions 1–5 progress, migration approach, example callouts.          | No spec content lives here. Update only if adding a new historical chapter or correcting the dated header.                                                             |
| `Work Vault/03. Resources/OpenBrain Setup/Getting Started — New Machine Setup.md`     | Setup guide. One-line rubric mention pointing here.                                              | No action unless the one-line summary becomes inaccurate.                                                                                                              |
| `~/.claude/plugins/ght-vault/skills/improve-thought/SKILL.md` (runtime install)        | The installed plugin path on each user's machine.                                                | Re-run `setup.sh` to copy the updated vault skill file into the home directory.                                                                                        |
| `Work Vault/00. Inbox/La Cosecha — Convention Brain Game.md` (Appendix A — El Coach prompt) | **Operational mirror for a future agent.** Embeds the rubric inline because El Coach's prompt must be self-contained at deploy time. Treat like `curate.py:SYSTEM_PROMPT` — paired edit required. | Update the SCORING RUBRIC section to match the current canonical spec. Self-contained prose is required (no pointers in the agent prompt itself). |
| Memory: `openbrain_rubric_v3.md`                                                       | Memory pointer for cross-session continuity.                                                     | Bump version reference and date. Replace the pointer if the file path here changes.                                                                                    |

### 11.3 Verification step after any rubric edit

After propagating, run these greps from the vault root to confirm no orphaned spec prose remains outside the canonical sites.

**Weight pattern (dimensions/composite):**

```bash
grep -rln "0\.25.*0\.20.*0\.20.*0\.15.*0\.10.*0\.10" \
  --include="*.md" --include="*.py" \
  --exclude-dir=.obsidian --exclude-dir=.smart-env --exclude-dir=.venv .
```

Expected hits: `_rubric.md`, `curate.py`, `rescore.py`, `_claude-context.md`, `.claude/plugins/ght-vault/skills/improve-thought/SKILL.md`, `Work Vault/00. Inbox/La Cosecha — Convention Brain Game.md`, `Work Vault/03. Resources/OpenBrain Setup/PKM Workflow — Vault to Brain Loop.md`, `Work Vault/03. Resources/OpenBrain Setup/obsidian-supabase-brain-roadmap.md`. Anything else is drift — fix it.

**KIND pattern (categorical classification — v3.2 +):**

```bash
grep -rln "kind:fact\|kind:hypothesis\|kind:principle\|kind:aspiration\|kind:question\|\\bfact \| hypothesis \| principle \| aspiration \| question\\b" \
  --include="*.md" --include="*.py" \
  --exclude-dir=.obsidian --exclude-dir=.smart-env --exclude-dir=.venv .
```

Expected hits after full propagation: same files as above plus actual curated notes that have been re-touched under v3.2. Spec prose lives in `_rubric.md` (§4), `curate.py` (`SYSTEM_PROMPT`), `rescore.py` (`SYSTEM_PROMPT`), `upload.py` (validator), and the user-facing summaries in `_claude-context.md`, `SKILL.md`, `PKM Workflow`, and `La Cosecha` Appendix A. Anything else is drift.

---

## 12. What this rubric is NOT

- **Not a thought-generation tool.** The rubric scores extracted thoughts; it does not produce them. Extraction is upstream and follows §8.
- **Not a strategy framework.** It measures whether a thought is well-distilled, not whether the underlying strategy is correct. A perfectly-scored thought can describe a wrong-headed decision.
- **Not stable across versions.** The rubric has been versioned six times in two days (v1, v2, v3, v3.1, v3.2, v3.3 across 2026-05-15 and 2026-05-16). Treat the current weights, dimensions, KIND set, and marker rendering as load-bearing only for the current sweep — expect future revisions, and design any tooling around §7's migration model rather than assuming v3.3 is permanent.
- **Not the whole ontology.** v3.3 scores only the *thought* layer. The agreed long-term direction is a three-layer ontology (thought → insight → meaning), each layer needing its own rubric — see §13.

---

## 13. Roadmap — Layered Ontology (thoughts → insights → meaning)

This section documents forward-looking rubric work that is **not yet active in v3.3**. The framing originated in `Work Vault/00. Inbox/La Cosecha — Convention Brain Game.md` §16 ("Beyond v1 — Open Items for Future Conversation") and is restated here because each item will require its own rubric, marker format, and propagation discipline — making `_rubric.md` the canonical design home for the layered ontology.

### 13.1 The Three Layers

v3.3 captures **thoughts only**. The agreed long-term direction is:

| Layer       | Definition                                                                                                                | Rubric status                                                                       |
|-------------|---------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------|
| **Thought** | An atomic, assertable claim. The unit scored by the v3.3 rubric (§3, §4, §5).                                              | **Active.** This document is the canonical spec.                                    |
| **Insight** | A new, assertable claim born from the **juxtaposition of two or more thoughts**. Emergent — not present in any single source thought. | Designed in concept; not yet built. Will need its own rubric and marker schema.     |
| **Meaning** | A description of a domain assembled from a collection of insights. The highest aggregation level — typically scoped to a Capítulo del Saber (functional chapter). | Designed in concept; not yet built. Will need its own rubric and marker schema.     |

### 13.2 Why Higher-Order Objects Need Their Own Rubrics

The six dimensions that score a *thought* (SP, IE, AC, TR, CO, DU) are not the dimensions that score an *insight* or a *meaning*. The expected (provisional) axes:

- **Insight-level scoring** will likely care about **coherence** (does the insight reconcile its source thoughts without distortion?) and **emergent novelty** (does it carry information not already present in any single source thought?). These are orthogonal to the thought-level dimensions and would dilute the composite if forced into the same formula.
- **Meaning-level scoring** will likely care about **completeness of domain coverage** (do the insights together describe the full territory?) and **stability** (does the meaning hold across temporal slices and personnel changes?).

The `kind:` taxonomy (§4) is thought-level only. Insights and meaning will need their own categorical taxonomies; some thought kinds (e.g., `question`) may not have direct analogues at higher layers.

### 13.3 Schema Implications (when activated)

- **Insight callouts** will be a new first-class object type — likely `[!insight]` callouts that link by `^anchorid` to the 2+ source thoughts they emerge from. The source links are **load-bearing**: an insight without source thoughts is not yet evidenced.
- **Meaning entries** will reference a set of insights plus a domain identifier (probably one of the existing Capítulos del Saber, given the chapter taxonomy already in use).
- An **"insight-spotting" contribution type** will reward the participant or agent who connects two thoughts that produce something new — distinct from authoring an atomic thought.

### 13.4 Canonical Record Evolution (preconditions for the layered ontology)

Insights only form coherently when the underlying canonical record is internally consistent. Two adjacent roadmap items are preconditions:

- **Contradiction resolution by succession.** When two canonical entries contradict, the newer entry either **supersedes** the older (`supersedes:` pointer making lineage transparent) or **complements** it (explicit framing of where each holds). Succession, not co-existence — mirroring how individual thinking evolves rather than letting the weave accumulate confidently-asserted contradictions. The procedural mechanics (who can raise a contradiction, what evidence is required, cadence of review) are part of the v2 design.
- **Thought deprecation by succession.** Every canonical entry carries a `supersedes:` field (initially empty). When a newer thought is judged to capture the same territory more truthfully, the pointer is added; the older thought is demoted from the active canonical set but remains queryable as historical provenance. Succession, not deletion — preserves the lineage of how GHT's understanding evolved.

Both will require new marker fields (`supersedes:`, possibly `superseded_by:`) and parser updates analogous to the v3.2 KIND addition and v3.3 multi-line block — i.e., they are versioned spec changes, not free-form metadata.

### 13.5 Retrieval (the other half of the layered ontology)

v3.3 designs capture exhaustively and retrieval not at all. Insights and meaning are only useful if retrieval surfaces them at the moment of need — through skill invocations, a dedicated KM agent (e.g., `/ask-brain`), and ambient context auto-injection in Cortex and similar surfaces. The Tejido Candor pattern (La Cosecha §15) is the retrieval-side rule: when the weave doesn't know, it says so. The full retrieval workstream (ranking, relevance, query interface, coverage confidence display, contradiction surfacing) is a separate v2 design effort and is documented here because retrieval semantics constrain how insight and meaning markers must encode their source links.

### 13.6 Propagation Constraint When Activated

When the layered ontology activates, every propagation target in §11.2 must be revisited. Insights and meaning will need:

- their own marker format(s) in this document (probably §6.x sub-sections per layer);
- their own classifier prompts in `curate.py` / `rescore.py`, or dedicated scripts;
- their own upload path in `upload.py` (the brain schema will need new object types);
- their own retrieval semantics in any agent that queries the weave.

Treat this section as a **placeholder for the architectural commitments** that bind the v2 design. The full design conversation is parked to the v2 / Cosecha Continua agenda — La Cosecha §16 carries the broader context (governance, seed-pool drafting, Curation Council federalization, ongoing operations) that is out of scope here.
