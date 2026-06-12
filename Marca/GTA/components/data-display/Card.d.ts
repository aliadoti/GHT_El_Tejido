import * as React from "react";

/**
 * The fundamental surface: white, sand hairline, soft shadow, lg radius.
 * @startingPoint section="Components" subtitle="Card — eyebrow, title, body, footer" viewport="700x260"
 */
export interface CardProps {
  children?: React.ReactNode;
  /** Uppercase overline above the title. */
  eyebrow?: string;
  /** Garet-heavy title. */
  title?: React.ReactNode;
  /** Footer slot (actions, meta). */
  footer?: React.ReactNode;
  /** Lift + deepen shadow on hover. @default false */
  interactive?: boolean;
  /** Inner padding in px. @default 24 */
  padding?: number;
  style?: React.CSSProperties;
}

/** The fundamental surface: white, sand hairline, soft shadow, lg radius. */
export function Card(props: CardProps): JSX.Element;
