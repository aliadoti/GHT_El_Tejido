import * as React from "react";

export interface IconButtonProps {
  /** Icon node (Lucide SVG/<i>, etc). */
  icon: React.ReactNode;
  /** Accessible label (also the tooltip title). */
  label: string;
  /** @default "ghost" */
  variant?: "primary" | "secondary" | "ghost";
  /** @default "md" */
  size?: "sm" | "md" | "lg";
  disabled?: boolean;
  onClick?: (e: React.MouseEvent<HTMLButtonElement>) => void;
  style?: React.CSSProperties;
}

/** Square icon-only action sharing the Button visual language. */
export function IconButton(props: IconButtonProps): JSX.Element;
