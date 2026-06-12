import React from "react";

/**
 * Aliado TI — Badge
 * Small status pill. Tones map to the muted status palette + neutral.
 */
export function Badge({ children, tone = "neutral", variant = "soft", dot = false, style }) {
  const tones = {
    neutral: { fg: "var(--warm-gray-700)", bg: "var(--cream-200)", solid: "var(--warm-gray-600)" },
    info:    { fg: "var(--status-info-fg)", bg: "var(--status-info-bg)", solid: "var(--status-info-solid)" },
    success: { fg: "var(--status-success-fg)", bg: "var(--status-success-bg)", solid: "var(--status-success-solid)" },
    warning: { fg: "var(--status-warning-fg)", bg: "var(--status-warning-bg)", solid: "var(--status-warning-solid)" },
    danger:  { fg: "var(--status-danger-fg)", bg: "var(--status-danger-bg)", solid: "var(--status-danger-solid)" },
  };
  const t = tones[tone] || tones.neutral;
  const solid = variant === "solid";

  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: 6,
      height: 22, padding: "0 9px",
      borderRadius: "var(--radius-pill)",
      fontFamily: "var(--font-body)", fontSize: "var(--text-xs)", fontWeight: 700,
      lineHeight: 1, whiteSpace: "nowrap",
      color: solid ? "#fff" : t.fg,
      background: solid ? t.solid : t.bg,
      ...style,
    }}>
      {dot ? <span style={{ width: 6, height: 6, borderRadius: "50%", background: solid ? "#fff" : t.solid }} /> : null}
      {children}
    </span>
  );
}
