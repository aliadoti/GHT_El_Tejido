import * as React from "react";

export interface TagProps {
  children?: React.ReactNode;
  /** When provided, renders a remove (×) button. */
  onRemove?: () => void;
  leadingIcon?: React.ReactNode;
  style?: React.CSSProperties;
}

/** Neutral chip, optionally removable. For filters, áreas, document types. */
export function Tag(props: TagProps): JSX.Element;
