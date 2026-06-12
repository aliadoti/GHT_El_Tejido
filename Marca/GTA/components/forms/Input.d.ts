import * as React from "react";

/**
 * Single-line text field with label, hint, error and adornments.
 * @startingPoint section="Components" subtitle="Form fields — input, select, checkbox, switch" viewport="700x360"
 */
export interface InputProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, "size"> {
  label?: string;
  hint?: string;
  /** Error message; also turns the border red. */
  error?: string;
  leading?: React.ReactNode;
  trailing?: React.ReactNode;
  /** @default "md" */
  size?: "sm" | "md" | "lg";
  disabled?: boolean;
}

/** Single-line text field with label, hint, error and adornments. */
export function Input(props: InputProps): JSX.Element;
