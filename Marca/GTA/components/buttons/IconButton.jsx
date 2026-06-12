import React from "react";

/**
 * Aliado TI — IconButton
 * Square, icon-only action. Same visual language as Button.
 */
export function IconButton({
  icon,
  label,
  variant = "ghost",
  size = "md",
  disabled = false,
  onClick,
  style,
  ...rest
}) {
  const dims = { sm: 34, md: 42, lg: 50 };
  const d = dims[size] || dims.md;

  const variants = {
    primary: { background: "var(--action-primary-bg)", color: "var(--action-primary-text)", border: "1px solid transparent" },
    secondary: { background: "var(--action-secondary-bg)", color: "var(--action-secondary-text)", border: "1px solid var(--action-secondary-border)" },
    ghost: { background: "transparent", color: "var(--action-ghost-text)", border: "1px solid transparent" },
  };

  const hovers = {
    primary: "var(--action-primary-bg-hover)",
    secondary: "var(--action-secondary-bg-hover)",
    ghost: "var(--action-ghost-bg-hover)",
  };

  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
      style={{
        width: d,
        height: d,
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        borderRadius: "var(--radius-sm)",
        cursor: disabled ? "not-allowed" : "pointer",
        opacity: disabled ? 0.45 : 1,
        transition: "background var(--duration-fast) var(--ease-out)",
        ...(variants[variant] || variants.ghost),
        ...style,
      }}
      onMouseEnter={(e) => !disabled && (e.currentTarget.style.background = hovers[variant] || "transparent")}
      onMouseLeave={(e) => !disabled && (e.currentTarget.style.background = variant === "ghost" ? "transparent" : (variants[variant]?.background))}
      {...rest}
    >
      {icon}
    </button>
  );
}
