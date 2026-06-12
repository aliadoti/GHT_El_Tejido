import React from "react";

/**
 * Aliado TI — Alert
 * Inline message banner. Tones map to the status palette.
 */
export function Alert({ children, title, tone = "info", icon = null, onClose, style }) {
  const tones = {
    info:    { fg: "var(--status-info-fg)", bg: "var(--status-info-bg)", bar: "var(--status-info-solid)" },
    success: { fg: "var(--status-success-fg)", bg: "var(--status-success-bg)", bar: "var(--status-success-solid)" },
    warning: { fg: "var(--status-warning-fg)", bg: "var(--status-warning-bg)", bar: "var(--status-warning-solid)" },
    danger:  { fg: "var(--status-danger-fg)", bg: "var(--status-danger-bg)", bar: "var(--status-danger-solid)" },
  };
  const t = tones[tone] || tones.info;

  return (
    <div role="alert" style={{
      display: "flex", gap: 12,
      padding: "14px 16px",
      background: t.bg,
      borderRadius: "var(--radius-md)",
      borderLeft: `3px solid ${t.bar}`,
      ...style,
    }}>
      {icon ? <span style={{ display: "inline-flex", color: t.bar, marginTop: 1 }}>{icon}</span> : null}
      <div style={{ flex: 1, minWidth: 0 }}>
        {title ? (
          <div style={{ fontFamily: "var(--font-body)", fontWeight: 700, fontSize: "var(--text-sm)", color: t.fg, marginBottom: children ? 3 : 0 }}>{title}</div>
        ) : null}
        {children ? (
          <div style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-sm)", color: "var(--text-secondary)", lineHeight: 1.5 }}>{children}</div>
        ) : null}
      </div>
      {onClose ? (
        <button type="button" aria-label="Cerrar" onClick={onClose} style={{
          border: "none", background: "transparent", cursor: "pointer", color: "var(--text-muted)",
          display: "inline-flex", padding: 2, height: "fit-content",
        }}>
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg>
        </button>
      ) : null}
    </div>
  );
}
