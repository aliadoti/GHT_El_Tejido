import React from "react";

/**
 * Aliado TI — Tabs
 * Underline tabs. Controlled via value/onChange or uncontrolled.
 */
export function Tabs({ tabs = [], value, defaultValue, onChange, style }) {
  const isControlled = value !== undefined;
  const [internal, setInternal] = React.useState(defaultValue ?? (tabs[0] && tabs[0].id));
  const active = isControlled ? value : internal;

  const select = (id) => {
    if (!isControlled) setInternal(id);
    onChange && onChange(id);
  };

  return (
    <div role="tablist" style={{ display: "flex", gap: 4, borderBottom: "1px solid var(--border-subtle)", ...style }}>
      {tabs.map((t) => {
        const on = t.id === active;
        return (
          <button key={t.id} role="tab" aria-selected={on} onClick={() => select(t.id)} style={{
            position: "relative",
            display: "inline-flex", alignItems: "center", gap: 7,
            padding: "10px 14px",
            border: "none", background: "transparent", cursor: "pointer",
            fontFamily: "var(--font-body)", fontSize: "var(--text-sm)",
            fontWeight: on ? 700 : 600,
            color: on ? "var(--text-primary)" : "var(--text-muted)",
            transition: "color var(--duration-fast) var(--ease-out)",
          }}
          onMouseEnter={(e) => { if (!on) e.currentTarget.style.color = "var(--text-secondary)"; }}
          onMouseLeave={(e) => { if (!on) e.currentTarget.style.color = "var(--text-muted)"; }}>
            {t.icon ? <span style={{ display: "inline-flex" }}>{t.icon}</span> : null}
            {t.label}
            {t.count !== undefined ? (
              <span style={{ fontSize: "var(--text-xs)", fontWeight: 700, color: on ? "var(--text-primary)" : "var(--text-muted)", background: "var(--cream-200)", borderRadius: "var(--radius-pill)", padding: "1px 7px" }}>{t.count}</span>
            ) : null}
            <span style={{
              position: "absolute", left: 6, right: 6, bottom: -1, height: 2,
              borderRadius: "2px 2px 0 0",
              background: on ? "var(--ink-900)" : "transparent",
              transition: "background var(--duration-fast) var(--ease-out)",
            }} />
          </button>
        );
      })}
    </div>
  );
}
