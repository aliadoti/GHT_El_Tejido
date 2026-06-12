import React from "react";

/**
 * Aliado TI — Button
 * Editorial-luxury button: charcoal-ink primary on cream, quiet hovers,
 * no bounce. Variants: primary | secondary | ghost | danger.
 */
export function Button({
  children,
  variant = "primary",
  size = "md",
  type = "button",
  disabled = false,
  fullWidth = false,
  leadingIcon = null,
  trailingIcon = null,
  onClick,
  style,
  ...rest
}) {
  const sizes = {
    sm: { height: 34, padding: "0 14px", font: "var(--text-sm)", radius: "var(--radius-sm)", gap: 6 },
    md: { height: 42, padding: "0 20px", font: "var(--text-sm)", radius: "var(--radius-sm)", gap: 8 },
    lg: { height: 50, padding: "0 26px", font: "var(--text-base)", radius: "var(--radius-md)", gap: 10 },
  };
  const s = sizes[size] || sizes.md;

  const base = {
    display: "inline-flex",
    alignItems: "center",
    justifyContent: "center",
    gap: s.gap,
    height: s.height,
    padding: s.padding,
    width: fullWidth ? "100%" : "auto",
    fontFamily: "var(--font-body)",
    fontWeight: 700,
    fontSize: s.font,
    letterSpacing: "0.01em",
    lineHeight: 1,
    borderRadius: s.radius,
    border: "1px solid transparent",
    cursor: disabled ? "not-allowed" : "pointer",
    opacity: disabled ? 0.45 : 1,
    transition: "background var(--duration-fast) var(--ease-out), border-color var(--duration-fast) var(--ease-out), transform var(--duration-fast) var(--ease-out)",
    whiteSpace: "nowrap",
    userSelect: "none",
  };

  const variants = {
    primary: { background: "var(--action-primary-bg)", color: "var(--action-primary-text)" },
    secondary: {
      background: "var(--action-secondary-bg)",
      color: "var(--action-secondary-text)",
      borderColor: "var(--action-secondary-border)",
    },
    ghost: { background: "transparent", color: "var(--action-ghost-text)" },
    danger: { background: "var(--status-danger-solid)", color: "#fff" },
  };

  const hoverFor = {
    primary: (e, on) => (e.currentTarget.style.background = on ? "var(--action-primary-bg-hover)" : "var(--action-primary-bg)"),
    secondary: (e, on) => (e.currentTarget.style.background = on ? "var(--action-secondary-bg-hover)" : "var(--action-secondary-bg)"),
    ghost: (e, on) => (e.currentTarget.style.background = on ? "var(--action-ghost-bg-hover)" : "transparent"),
    danger: (e, on) => (e.currentTarget.style.background = on ? "var(--red-700)" : "var(--status-danger-solid)"),
  };

  return (
    <button
      type={type}
      disabled={disabled}
      onClick={onClick}
      style={{ ...base, ...(variants[variant] || variants.primary), ...style }}
      onMouseEnter={(e) => !disabled && hoverFor[variant]?.(e, true)}
      onMouseLeave={(e) => !disabled && hoverFor[variant]?.(e, false)}
      onMouseDown={(e) => !disabled && (e.currentTarget.style.transform = "translateY(1px)")}
      onMouseUp={(e) => !disabled && (e.currentTarget.style.transform = "translateY(0)")}
      {...rest}
    >
      {leadingIcon ? <span style={{ display: "inline-flex" }}>{leadingIcon}</span> : null}
      {children}
      {trailingIcon ? <span style={{ display: "inline-flex" }}>{trailingIcon}</span> : null}
    </button>
  );
}
