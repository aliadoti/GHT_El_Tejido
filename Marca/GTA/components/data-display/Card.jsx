import React from "react";

/**
 * Aliado TI — Card
 * The fundamental surface. White, sand hairline, soft shadow, lg radius.
 * Optional eyebrow + title header and footer slot.
 */
export function Card({ children, eyebrow, title, footer, interactive = false, padding = 24, style }) {
  const [hover, setHover] = React.useState(false);
  return (
    <div
      onMouseEnter={() => interactive && setHover(true)}
      onMouseLeave={() => interactive && setHover(false)}
      style={{
        display: "flex", flexDirection: "column",
        background: "var(--surface-card)",
        border: "1px solid var(--border-subtle)",
        borderRadius: "var(--radius-card)",
        boxShadow: hover ? "var(--shadow-lg)" : "var(--shadow-sm)",
        transform: hover ? "translateY(-2px)" : "none",
        transition: "box-shadow var(--duration-normal) var(--ease-out), transform var(--duration-normal) var(--ease-out)",
        cursor: interactive ? "pointer" : "default",
        overflow: "hidden",
        ...style,
      }}
    >
      {(eyebrow || title) ? (
        <div style={{ padding: `${padding}px ${padding}px 0` }}>
          {eyebrow ? (
            <div style={{ fontFamily: "var(--font-body)", fontWeight: 700, letterSpacing: "0.14em", textTransform: "uppercase", fontSize: "var(--text-xs)", color: "var(--text-muted)", marginBottom: 8 }}>{eyebrow}</div>
          ) : null}
          {title ? (
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 900, fontSize: "var(--text-xl)", letterSpacing: "-0.01em", color: "var(--text-primary)" }}>{title}</div>
          ) : null}
        </div>
      ) : null}
      <div style={{ padding }}>{children}</div>
      {footer ? (
        <div style={{ padding: `0 ${padding}px ${padding}px`, marginTop: "auto" }}>{footer}</div>
      ) : null}
    </div>
  );
}
