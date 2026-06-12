import * as React from "react";

/**
 * Props for the primary action control.
 * @startingPoint section="Components" subtitle="Buttons — primary, secondary, ghost, danger" viewport="700x180"
 */
export interface ButtonProps {
  children?: React.ReactNode;
  /** Visual style. @default "primary" */
  variant?: "primary" | "secondary" | "ghost" | "danger";
  /** @default "md" */
  size?: "sm" | "md" | "lg";
  type?: "button" | "submit" | "reset";
  disabled?: boolean;
  /** Stretch to container width. @default false */
  fullWidth?: boolean;
  /** Icon node rendered before the label (e.g. a Lucide <i>/SVG). */
  leadingIcon?: React.ReactNode;
  /** Icon node rendered after the label. */
  trailingIcon?: React.ReactNode;
  onClick?: (e: React.MouseEvent<HTMLButtonElement>) => void;
  style?: React.CSSProperties;
}

/** The primary action control. Charcoal-ink primary on cream; quiet hover, no bounce. */
export function Button(props: ButtonProps): JSX.Element;
