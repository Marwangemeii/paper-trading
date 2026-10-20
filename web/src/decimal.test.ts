import { describe, expect, it } from 'vitest';
import { compareDecimal, decimalPlaces, formatDecimal, isMultipleOf, sign } from './decimal';

describe('isMultipleOf', () => {
  it('accepts exact multiples of the step', () => {
    expect(isMultipleOf('0.1', '0.00001')).toBe(true);
    expect(isMultipleOf('3400', '0.01')).toBe(true);
    expect(isMultipleOf('3400.25', '0.01')).toBe(true);
    expect(isMultipleOf('0.3', '0.1')).toBe(true); // 0.1 + 0.2 issue does not apply
  });

  it('rejects values finer than the step', () => {
    expect(isMultipleOf('0.000001', '0.00001')).toBe(false);
    expect(isMultipleOf('3400.255', '0.01')).toBe(false);
    expect(isMultipleOf('1.5', '1')).toBe(false);
  });

  it('handles non-power-of-ten steps and bad input', () => {
    expect(isMultipleOf('1.5', '0.5')).toBe(true);
    expect(isMultipleOf('1.25', '0.5')).toBe(false);
    expect(isMultipleOf('abc', '0.01')).toBe(false);
    expect(isMultipleOf('1', '0')).toBe(false);
  });
});

describe('formatDecimal', () => {
  it('adds thousands separators without losing precision', () => {
    expect(formatDecimal('93827.485186')).toBe('93,827.485186');
    expect(formatDecimal('123456789012345678.123456789')).toBe('123,456,789,012,345,678.123456789');
    expect(formatDecimal('999')).toBe('999');
  });

  it('rounds half away from zero to maxFrac', () => {
    expect(formatDecimal('93827.485186', { maxFrac: 2 })).toBe('93,827.49');
    expect(formatDecimal('999.995', { maxFrac: 2 })).toBe('1,000');
    expect(formatDecimal('-12.345', { maxFrac: 2, minFrac: 2 })).toBe('-12.35');
    expect(formatDecimal('0.004', { maxFrac: 2, minFrac: 2 })).toBe('0.00');
  });

  it('pads, signs and trims', () => {
    expect(formatDecimal('5', { minFrac: 2 })).toBe('5.00');
    expect(formatDecimal('1.500000')).toBe('1.5');
    expect(formatDecimal('12.5', { signed: true })).toBe('+12.5');
    expect(formatDecimal('-0.00', { signed: true })).toBe('0');
    expect(formatDecimal(null)).toBe('—');
  });
});

describe('sign / compareDecimal / decimalPlaces', () => {
  it('works on strings', () => {
    expect(sign('-0.01')).toBe(-1);
    expect(sign('0.000')).toBe(0);
    expect(sign('2')).toBe(1);
    expect(compareDecimal('66935.65', '66935.6')).toBe(1);
    expect(compareDecimal('-1', '0.5')).toBe(-1);
    expect(compareDecimal('1.10', '1.1')).toBe(0);
    expect(decimalPlaces('0.01')).toBe(2);
    expect(decimalPlaces('1')).toBe(0);
  });
});
