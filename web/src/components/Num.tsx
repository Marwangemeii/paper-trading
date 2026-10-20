import { sign } from '../decimal';

interface NumProps {
  /** Already formatted text to display. */
  text: string;
  /** Exact raw value, shown as a tooltip. */
  raw?: string | null;
  /** Colour green/red by the sign of this value. */
  pnl?: string | null;
}

/** Numeric text that always renders left-to-right (also inside RTL layouts). */
export function Num({ text, raw, pnl }: NumProps) {
  const s = pnl === undefined ? 0 : sign(pnl);
  const cls = s > 0 ? 'num pos' : s < 0 ? 'num neg' : 'num';
  return (
    <span className={cls} dir="ltr" title={raw ?? undefined}>
      {text}
    </span>
  );
}
