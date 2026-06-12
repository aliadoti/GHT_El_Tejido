import * as React from "react";

export interface BadgeProps {
  children?: React.ReactNode;
  /** @default "neutral" */
  tone?: "neutral" | "info" | "success" | "warning" | "danger";
  /** soft tint or solid fill. @default "soft" */
  variant?: "soft" | "solid";
  /** Show a leading status dot. @default false */
  dot?: boolean;
  style?: React.CSSProperties;
}

/** Small status pill. Use for supplier states: Solicitado, Firmado, Devuelto… */
export function Badge(props: BadgeProps): JSX.Element;
