import React from "react";

/**
 * Aliado TI — Textarea
 * Multi-line field matching Input's styling.
 */
export function Textarea({ label, hint, error, rows = 4, disabled = false, id, style, ...rest }) {
  const [focus, setFocus] = React.useState(false);
  const reactId = React.useId();
  const taId = id || reactId;
  const borderColor = error ? "var(--status-danger-solid)" : focus ? "var(--border-focus)" : "var(--border-default)";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6, width: "100%", ...style }}>
      {label ? (
        <label htmlFor={taId} style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-sm)", fontWeight: 700, color: "var(--text-primary)" }}>{label}</label>
      ) : null}
      <textarea
        id={taId}
        rows={rows}
        disabled={disabled}
        onFocus={() => setFocus(true)}
        onBlur={() => setFocus(false)}
        style={{
          width: "100%",
          resize: "vertical",
          padding: "12px 14px",
          background: disabled ? "var(--surface-sunken)" : "var(--surface-card)",
          border: `1px solid ${borderColor}`,
          borderRadius: "var(--radius-sm)",
          boxShadow: focus ? "var(--focus-ring)" : "none",
          fontFamily: "var(--font-body)",
          fontSize: "var(--text-sm)",
          lineHeight: 1.5,
          color: "var(--text-primary)",
          outline: "none",
          transition: "border-color var(--duration-fast) var(--ease-out), box-shadow var(--duration-fast) var(--ease-out)",
        }}
        {...rest}
      />
      {error ? (
        <span style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-xs)", color: "var(--status-danger-fg)" }}>{error}</span>
      ) : hint ? (
        <span style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>{hint}</span>
      ) : null}
    </div>
  );
}
