import React from "react";

/**
 * Aliado TI — Checkbox
 * Controlled or uncontrolled square checkbox with ink fill.
 */
export function Checkbox({ label, checked, defaultChecked, disabled = false, onChange, id, style, ...rest }) {
  const reactId = React.useId();
  const cbId = id || reactId;
  const isControlled = checked !== undefined;
  const [internal, setInternal] = React.useState(defaultChecked || false);
  const on = isControlled ? checked : internal;

  const toggle = (e) => {
    if (disabled) return;
    if (!isControlled) setInternal(e.target.checked);
    onChange && onChange(e);
  };

  return (
    <label htmlFor={cbId} style={{ display: "inline-flex", alignItems: "center", gap: 10, cursor: disabled ? "not-allowed" : "pointer", opacity: disabled ? 0.5 : 1, ...style }}>
      <span style={{ position: "relative", width: 20, height: 20, flex: "none" }}>
        <input id={cbId} type="checkbox" checked={on} disabled={disabled} onChange={toggle}
          style={{ position: "absolute", opacity: 0, width: 20, height: 20, margin: 0, cursor: "inherit" }} {...rest} />
        <span style={{
          display: "flex", alignItems: "center", justifyContent: "center",
          width: 20, height: 20, borderRadius: "var(--radius-xs)",
          background: on ? "var(--ink-900)" : "var(--surface-card)",
          border: `1.5px solid ${on ? "var(--ink-900)" : "var(--border-strong)"}`,
          transition: "all var(--duration-fast) var(--ease-out)",
        }}>
          {on ? (
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="var(--cream-50)" strokeWidth="3.2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M20 6 9 17l-5-5" />
            </svg>
          ) : null}
        </span>
      </span>
      {label ? <span style={{ fontFamily: "var(--font-body)", fontSize: "var(--text-sm)", color: "var(--text-primary)" }}>{label}</span> : null}
    </label>
  );
}
