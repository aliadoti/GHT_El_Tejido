import React from "react";

/**
 * Aliado TI — Input
 * Text field with optional label, hint, error and leading/trailing adornments.
 */
export function Input({
  label,
  hint,
  error,
  leading = null,
  trailing = null,
  size = "md",
  disabled = false,
  id,
  style,
  ...rest
}) {
  const [focus, setFocus] = React.useState(false);
  const reactId = React.useId();
  const inputId = id || reactId;
  const heights = { sm: 36, md: 44, lg: 52 };
  const h = heights[size] || heights.md;
  const borderColor = error
    ? "var(--status-danger-solid)"
    : focus
    ? "var(--border-focus)"
    : "var(--border-default)";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6, width: "100%", ...style }}>
      {label ? (
        <label htmlFor={inputId} style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-sm)", fontWeight: 700, color: "var(--text-primary)" }}>
          {label}
        </label>
      ) : null}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          height: h,
          padding: "0 14px",
          background: disabled ? "var(--surface-sunken)" : "var(--surface-card)",
          border: `1px solid ${borderColor}`,
          borderRadius: "var(--radius-sm)",
          boxShadow: focus ? "var(--focus-ring)" : "none",
          transition: "border-color var(--duration-fast) var(--ease-out), box-shadow var(--duration-fast) var(--ease-out)",
          opacity: disabled ? 0.6 : 1,
        }}
      >
        {leading ? <span style={{ display: "inline-flex", color: "var(--text-muted)" }}>{leading}</span> : null}
        <input
          id={inputId}
          disabled={disabled}
          onFocus={() => setFocus(true)}
          onBlur={() => setFocus(false)}
          style={{
            flex: 1,
            minWidth: 0,
            border: "none",
            outline: "none",
            background: "transparent",
            fontFamily: "var(--font-body)",
            fontSize: "var(--text-sm)",
            color: "var(--text-primary)",
          }}
          {...rest}
        />
        {trailing ? <span style={{ display: "inline-flex", color: "var(--text-muted)" }}>{trailing}</span> : null}
      </div>
      {error ? (
        <span style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-xs)", color: "var(--status-danger-fg)" }}>{error}</span>
      ) : hint ? (
        <span style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>{hint}</span>
      ) : null}
    </div>
  );
}
