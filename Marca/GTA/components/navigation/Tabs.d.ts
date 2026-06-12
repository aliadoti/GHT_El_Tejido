import * as React from "react";

export interface TabItem {
  id: string;
  label: string;
  icon?: React.ReactNode;
  /** Optional count pill. */
  count?: number;
}

export interface TabsProps {
  tabs: TabItem[];
  /** Controlled active tab id. */
  value?: string;
  defaultValue?: string;
  onChange?: (id: string) => void;
  style?: React.CSSProperties;
}

/** Underline tabs. Controlled or uncontrolled. */
export function Tabs(props: TabsProps): JSX.Element;
