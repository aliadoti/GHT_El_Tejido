import * as React from "react";

export interface AlertProps {
  children?: React.ReactNode;
  title?: string;
  /** @default "info" */
  tone?: "info" | "success" | "warning" | "danger";
  /** Optional leading icon node. */
  icon?: React.ReactNode;
  /** When set, shows a close button. */
  onClose?: () => void;
  style?: React.CSSProperties;
}

/** Inline message banner with a left status bar. */
export function Alert(props: AlertProps): JSX.Element;
