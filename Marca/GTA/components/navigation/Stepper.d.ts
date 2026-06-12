import * as React from "react";

/**
 * Horizontal progress for multi-step flows — built for the supplier inscription wizard.
 * @startingPoint section="Components" subtitle="Stepper for multi-step wizards" viewport="700x250"
 */
export interface StepperProps {
  /** Array of strings or {label} objects. */
  steps: Array<string | StepItem>;
  /** Zero-based index of the active step. Earlier steps render as complete. */
  current: number;
  style?: React.CSSProperties;
}

export interface StepItem { label: string; }

/** Horizontal progress for multi-step flows — built for the supplier inscription wizard. */
export function Stepper(props: StepperProps): JSX.Element;
