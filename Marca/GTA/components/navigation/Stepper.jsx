import React from "react";

/**
 * Aliado TI — Stepper
 * Horizontal progress for multi-step flows (e.g. the 9-step inscription wizard).
 * Steps before `current` are complete (check); current is filled ink; later are muted.
 */
export function Stepper({ steps = [], current = 0, style }) {
  return (
    <div style={{ display: "flex", alignItems: "flex-start", width: "100%", ...style }}>
      {steps.map((s, i) => {
        const label = typeof s === "string" ? s : s.label;
        const done = i < current;
        const active = i === current;
        const isLast = i === steps.length - 1;
        const circleBg = done ? "var(--ink-900)" : active ? "var(--ink-900)" : "var(--surface-card)";
        const circleBorder = done || active ? "var(--ink-900)" : "var(--border-strong)";
        const circleColor = done || active ? "var(--cream-50)" : "var(--text-muted)";
        return (
          <div key={i} style={{ display: "flex", flexDirection: "column", alignItems: "center", flex: isLast ? "0 0 auto" : 1, minWidth: 0 }}>
            <div style={{ display: "flex", alignItems: "center", width: "100%" }}>
              <div style={{ flex: 1, height: 2, background: done ? "var(--ink-900)" : "transparent" }} />
              <div style={{
                display: "flex", alignItems: "center", justifyContent: "center",
                width: 30, height: 30, flex: "none",
                borderRadius: "var(--radius-pill)",
                background: circleBg, border: `1.5px solid ${circleBorder}`,
                color: circleColor,
                fontFamily: "var(--font-body)", fontWeight: 700, fontSize: "var(--text-sm)",
                boxShadow: active ? "var(--focus-ring)" : "none",
                transition: "all var(--duration-normal) var(--ease-out)",
              }}>
                {done ? (
                  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"><path d="M20 6 9 17l-5-5" /></svg>
                ) : (i + 1)}
              </div>
              <div style={{ flex: 1, height: 2, background: i < current ? "var(--ink-900)" : (isLast ? "transparent" : "var(--border-subtle)") }} />
            </div>
            <span style={{
              marginTop: 8, textAlign: "center", padding: "0 6px",
              fontFamily: "var(--font-body)", fontSize: "var(--text-xs)",
              fontWeight: active ? 700 : 600,
              color: active ? "var(--text-primary)" : done ? "var(--text-secondary)" : "var(--text-muted)",
              lineHeight: 1.25,
            }}>{label}</span>
          </div>
        );
      })}
    </div>
  );
}
