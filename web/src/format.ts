import type { Instrument } from './api';
import { decimalPlaces, formatDecimal } from './decimal';

/** "BTCUSDT" -> "BTC/USDT" (uses instrument metadata when available). */
export function displaySymbol(symbol: string, instrument?: Instrument): string {
  if (instrument) return `${instrument.baseAsset}/${instrument.quoteAsset}`;
  const m = /^(.+?)(USDT|USDC|USD|BTC|ETH)$/.exec(symbol);
  return m ? `${m[1]}/${m[2]}` : symbol;
}

export function baseAsset(symbol: string, instrument?: Instrument): string {
  return instrument?.baseAsset ?? displaySymbol(symbol).split('/')[0];
}

/** Price with the instrument's tick precision (always shows that many decimals). */
export function formatPrice(value: string | null | undefined, instrument?: Instrument): string {
  const places = instrument ? decimalPlaces(instrument.tickSize) : 2;
  return formatDecimal(value, { maxFrac: places, minFrac: places });
}

/** Quantity trimmed to the instrument's step precision. */
export function formatQty(value: string | null | undefined, instrument?: Instrument): string {
  return formatDecimal(value, { maxFrac: instrument ? decimalPlaces(instrument.quantityStep) : 8 });
}

/** Cash / values in the quote currency (2 decimals). */
export function formatMoney(value: string | null | undefined, signed = false): string {
  return formatDecimal(value, { maxFrac: 2, minFrac: 2, signed });
}
