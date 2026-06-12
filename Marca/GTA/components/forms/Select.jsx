import React from "react";

/**
 * Aliado TI — Select
 * Native select styled to match Input, with a custom chevron.
 */
export function Select({
  label,
  hint,
  error,
  options = [],
  placeholder,
  size = "md",
  disabled = false,
  id,
  style,
  ...rest
}) {
  const [focus, setFocus] = React.useState(false);
  const reactId = React.useId();
  const selId = id || reactId;
  const heights = { sm: 36, md: 44, lg: 52 };
  const h = heights[size] || heights.md;
  const borderColor = error ? "var(--status-danger-solid)" : focus ? "var(--border-focus)" : "var(--border-default)";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6, width: "100%", ...style }}>
      {label ? (
        <label htmlFor={selId} style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-sm)", fontWeight: 700, color: "var(--text-primary)" }}>
          {label}
        </label>
      ) : null}
      <div style={{ position: "relative", height: h }}>
        <select
          id={selId}
          disabled={disabled}
          onFocus={() => setFocus(true)}
          onBlur={() => setFocus(false)}
          style={{
            appearance: "none",
            WebkitAppearance: "none",
            width: "100%",
            height: h,
            padding: "0 38px 0 14px",
            background: disabled ? "var(--surface-sunken)" : "var(--surface-card)",
            border: `1px solid ${borderColor}`,
            borderRadius: "var(--radius-sm)",
            boxShadow: focus ? "var(--focus-ring)" : "none",
            fontFamily: "var(--font-body)",
            fontSize: "var(--text-sm)",
            color: "var(--text-primary)",
            outline: "none",
            cursor: disabled ? "not-allowed" : "pointer",
            transition: "border-color var(--duration-fast) var(--ease-out), box-shadow var(--duration-fast) var(--ease-out)",
          }}
          {...rest}
        >
          {placeholder ? <option value="">{placeholder}</option> : null}
          {options.map((o) => {
            const value = typeof o === "string" ? o : o.value;
            const lbl = typeof o === "string" ? o : o.label;
            return <option key={value} value={value}>{lbl}</option>;
          })}
        </select>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--text-muted)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
          style={{ position: "absolute", right: 13, top: "50%", transform: "translateY(-50%)", pointerEvents: "none" }}>
          <path d="m6 9 6 6 6-6" />
        </svg>
      </div>
      {error ? (
        <span style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-xs)", color: "var(--status-danger-fg)" }}>{error}</span>
      ) : hint ? (
        <span style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>{hint}</span>
      ) : null}
    </div>
  );
}
