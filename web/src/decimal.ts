// Exact decimal helpers that work on strings (no floating point).
// The API sends money and quantities as strings to keep exact decimals.

/** Unsigned decimal as typed by the user, e.g. "0.1" or "3400". */
export const UNSIGNED_DECIMAL = /^\d+(\.\d+)?$/;
const SIGNED_DECIMAL = /^-?\d+(\.\d+)?$/;

/** Number of fraction digits, ignoring trailing zeros ("0.0100" -> 2). */
export function decimalPlaces(value: string): number {
  const frac = value.split('.')[1] ?? '';
  return frac.replace(/0+$/, '').length;
}

/** "12.34" at scale 4 -> 123400n. Assumes an unsigned decimal string. */
function toScaled(value: string, scale: number): bigint {
  const [int, frac = ''] = value.split('.');
  return BigInt(int + frac.padEnd(scale, '0'));
}

/** True when value is an exact multiple of step (both unsigned decimals). */
export function isMultipleOf(value: string, step: string): boolean {
  if (!UNSIGNED_DECIMAL.test(value) || !UNSIGNED_DECIMAL.test(step)) return false;
  const scale = Math.max(value.split('.')[1]?.length ?? 0, step.split('.')[1]?.length ?? 0);
  const s = toScaled(step, scale);
  if (s === 0n) return false;
  return toScaled(value, scale) % s === 0n;
}

/** -1, 0 or 1 for a signed decimal string (0 for anything unparsable). */
export function sign(value: string | null | undefined): -1 | 0 | 1 {
  if (!value || !SIGNED_DECIMAL.test(value)) return 0;
  if (!/[1-9]/.test(value)) return 0;
  return value.startsWith('-') ? -1 : 1;
}

/** Compares two signed decimal strings exactly. */
export function compareDecimal(a: string, b: string): -1 | 0 | 1 {
  if (!SIGNED_DECIMAL.test(a) || !SIGNED_DECIMAL.test(b)) return 0;
  const scale = Math.max(a.split('.')[1]?.length ?? 0, b.split('.')[1]?.length ?? 0);
  const toBig = (v: string) => (v.startsWith('-') ? -toScaled(v.slice(1), scale) : toScaled(v, scale));
  const x = toBig(a);
  const y = toBig(b);
  return x === y ? 0 : x > y ? 1 : -1;
}

export interface FormatOptions {
  /** Round (half away from zero) to at most this many fraction digits. */
  maxFrac?: number;
  /** Keep at least this many fraction digits (pads with zeros). */
  minFrac?: number;
  /** Prefix positive values with "+". */
  signed?: boolean;
}

/**
 * Formats a decimal string with thousands separators using only string/BigInt math.
 * "93827.485186" -> "93,827.485186"; with { maxFrac: 2 } -> "93,827.49".
 */
export function formatDecimal(value: string | null | undefined, opts: FormatOptions = {}): string {
  if (value == null || value === '') return '—';
  if (!SIGNED_DECIMAL.test(value)) return value;

  const negative = value.startsWith('-');
  let [int, frac = ''] = (negative ? value.slice(1) : value).split('.');

  if (opts.maxFrac !== undefined && frac.length > opts.maxFrac) {
    const drop = frac.length - opts.maxFrac;
    const divisor = 10n ** BigInt(drop);
    const n = BigInt(int + frac);
    let q = n / divisor;
    if ((n % divisor) * 2n >= divisor) q += 1n;
    const digits = q.toString().padStart(opts.maxFrac + 1, '0');
    int = digits.slice(0, digits.length - opts.maxFrac);
    frac = digits.slice(digits.length - opts.maxFrac);
  }

  // Trim trailing zeros, but keep minFrac digits.
  const minFrac = opts.minFrac ?? 0;
  frac = frac.replace(/0+$/, '');
  if (frac.length < minFrac) frac = frac.padEnd(minFrac, '0');

  int = int.replace(/^0+(?=\d)/, '');
  const grouped = int.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  const body = frac ? `${grouped}.${frac}` : grouped;
  const isZero = !/[1-9]/.test(int + frac);

  if (negative && !isZero) return `-${body}`;
  if (opts.signed && !isZero) return `+${body}`;
  return body;
}
