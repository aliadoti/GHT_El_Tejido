import * as React from "react";

export interface CheckboxProps {
  label?: string;
  checked?: boolean;
  defaultChecked?: boolean;
  disabled?: boolean;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void;
  id?: string;
  style?: React.CSSProperties;
}

/** Square checkbox with ink fill. Controlled or uncontrolled. */
export function Checkbox(props: CheckboxProps): JSX.Element;
