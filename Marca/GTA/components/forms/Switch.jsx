import React from "react";

/**
 * Aliado TI — Switch
 * Pill toggle. Ink track when on.
 */
export function Switch({ label, checked, defaultChecked, disabled = false, onChange, id, style, ...rest }) {
  const reactId = React.useId();
  const swId = id || reactId;
  const isControlled = checked !== undefined;
  const [internal, setInternal] = React.useState(defaultChecked || false);
  const on = isControlled ? checked : internal;

  const toggle = (e) => {
    if (disabled) return;
    if (!isControlled) setInternal(e.target.checked);
    onChange && onChange(e);
  };

  return (
    <label htmlFor={swId} style={{ display: "inline-flex", alignItems: "center", gap: 10, cursor: disabled ? "not-allowed" : "pointer", opacity: disabled ? 0.5 : 1, ...style }}>
      <span style={{ position: "relative", width: 42, height: 24, flex: "none" }}>
        <input id={swId} type="checkbox" checked={on} disabled={disabled} onChange={toggle}
          style={{ position: "absolute", opacity: 0, width: 42, height: 24, margin: 0, cursor: "inherit" }} {...rest} />
        <span style={{
          display: "block", width: 42, height: 24, borderRadius: "var(--radius-pill)",
          background: on ? "var(--ink-900)" : "var(--sand-400)",
          transition: "background var(--duration-normal) var(--ease-out)",
        }} />
        <span style={{
          position: "absolute", top: 3, left: on ? 21 : 3, width: 18, height: 18,
          borderRadius: "var(--radius-pill)", background: "var(--white)",
          boxShadow: "var(--shadow-sm)",
          transition: "left var(--duration-normal) var(--ease-out)",
        }} />
      </span>
      {label ? <span style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-sm)", color: "var(--text-primary)" }}>{label}</span> : null}
    </label>
  );
}
