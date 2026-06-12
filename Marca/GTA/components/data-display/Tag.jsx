import React from "react";

/**
 * Aliado TI — Tag
 * Neutral chip, optionally removable.
 */
export function Tag({ children, onRemove, leadingIcon = null, style }) {
  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: 6,
      height: 28, padding: onRemove ? "0 6px 0 11px" : "0 11px",
      borderRadius: "var(--radius-sm)",
      background: "var(--surface-card)",
      border: "1px solid var(--border-default)",
      fontFamily: "var(--font-body)", fontSize: "var(--text-sm)", fontWeight: 600,
      color: "var(--text-primary)", whiteSpace: "nowrap",
      ...style,
    }}>
      {leadingIcon ? <span style={{ display: "inline-flex", color: "var(--text-muted)" }}>{leadingIcon}</span> : null}
      {children}
      {onRemove ? (
        <button type="button" aria-label="Quitar" onClick={onRemove} style={{
          display: "inline-flex", alignItems: "center", justifyContent: "center",
          width: 18, height: 18, padding: 0, border: "none", cursor: "pointer",
          borderRadius: "var(--radius-xs)", background: "transparent", color: "var(--text-muted)",
        }}
        onMouseEnter={(e) => (e.currentTarget.style.background = "var(--cream-200)")}
        onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}>
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg>
        </button>
      ) : null}
    </span>
  );
}
