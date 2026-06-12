import * as React from "react";

export interface SwitchProps {
  label?: string;
  checked?: boolean;
  defaultChecked?: boolean;
  disabled?: boolean;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void;
  id?: string;
  style?: React.CSSProperties;
}

/** Pill toggle with an ink track when on. Controlled or uncontrolled. */
export function Switch(props: SwitchProps): JSX.Element;
