import * as React from "react";

export interface AvatarProps {
  /** Full name — used for initials and alt text. */
  name?: string;
  /** Optional image URL; falls back to initials on the metal gradient. */
  src?: string;
  /** Pixel diameter. @default 40 */
  size?: number;
  style?: React.CSSProperties;
}

/** Round avatar: image or metallic-gradient initials. */
export function Avatar(props: AvatarProps): JSX.Element;
