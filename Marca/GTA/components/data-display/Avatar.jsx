import React from "react";

/**
 * Aliado TI — Avatar
 * Metallic-gradient initials avatar, or an image.
 */
export function Avatar({ name = "", src, size = 40, style }) {
  const initials = name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join("")
    .toUpperCase();

  return (
    <span style={{
      display: "inline-flex", alignItems: "center", justifyContent: "center",
      width: size, height: size, flex: "none",
      borderRadius: "var(--radius-pill)",
      background: src ? "var(--cream-200)" : "var(--gradient-metal)",
      color: "var(--cream-50)",
      fontFamily: "var(--font-body)", fontWeight: 700,
      fontSize: Math.round(size * 0.38),
      overflow: "hidden", userSelect: "none",
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.04)",
      ...style,
    }}>
      {src ? (
        <img src={src} alt={name} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
      ) : (
        initials || "·"
      )}
    </span>
  );
}
