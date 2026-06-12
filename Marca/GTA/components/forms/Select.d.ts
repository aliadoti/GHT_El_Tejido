import * as React from "react";

export interface SelectOption { value: string; label: string; }

export interface SelectProps extends Omit<React.SelectHTMLAttributes<HTMLSelectElement>, "size"> {
  label?: string;
  hint?: string;
  error?: string;
  /** Array of strings or {value,label} objects. */
  options?: Array<string | SelectOption>;
  placeholder?: string;
  /** @default "md" */
  size?: "sm" | "md" | "lg";
  disabled?: boolean;
}

/** Native select styled to match Input, with a custom chevron. */
export function Select(props: SelectProps): JSX.Element;
