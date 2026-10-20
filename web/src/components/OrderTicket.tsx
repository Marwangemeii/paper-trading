import { useState, type FormEvent } from 'react';
import { placeOrder, type Instrument, type Order, type OrderType, type Side } from '../api';
import { isMultipleOf, sign, UNSIGNED_DECIMAL } from '../decimal';
import { baseAsset, displaySymbol, formatMoney, formatPrice, formatQty } from '../format';
import { errorMessage } from '../errors';
import { reasonKey, useI18n, type MessageKey } from '../i18n';
import { Num } from './Num';

interface OrderTicketProps {
  accountId: string;
  instruments: Instrument[];
  symbol: string;
  onSymbolChange: (symbol: string) => void;
  lastPrice: string | null;
  availableCash: string | null;
  onPlaced: (order: Order) => void;
}

type Outcome = { kind: 'order'; order: Order; replayed: boolean } | { kind: 'error'; message: string };

const SIDES: Side[] = ['Buy', 'Sell'];
const TYPES: OrderType[] = ['Market', 'Limit'];

/** Returns an error message key, or null when the value is valid. */
function validate(value: string, step: string, stepError: MessageKey): MessageKey | null {
  if (value === '') return 'err.required';
  if (!UNSIGNED_DECIMAL.test(value)) return 'err.format';
  if (sign(value) <= 0) return 'err.positive';
  if (!isMultipleOf(value, step)) return stepError;
  return null;
}

export function OrderTicket({
  accountId,
  instruments,
  symbol,
  onSymbolChange,
  lastPrice,
  availableCash,
  onPlaced,
}: OrderTicketProps) {
  const { t } = useI18n();
  const [side, setSide] = useState<Side>('Buy');
  const [type, setType] = useState<OrderType>('Market');
  const [quantity, setQuantity] = useState('');
  const [limitPrice, setLimitPrice] = useState('');
  const [touched, setTouched] = useState({ quantity: false, limitPrice: false });
  const [submitting, setSubmitting] = useState(false);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const instrument = instruments.find((i) => i.symbol === symbol);
  const step = instrument?.quantityStep ?? '1';
  const tick = instrument?.tickSize ?? '1';
  const asset = baseAsset(symbol, instrument);

  const qtyError = validate(quantity, step, 'err.qtyStep');
  const priceError = type === 'Limit' ? validate(limitPrice, tick, 'err.priceTick') : null;
  const isValid = !!instrument && !qtyError && !priceError;

  const showQtyError = qtyError && (touched.quantity || quantity !== '');
  const showPriceError = priceError && (touched.limitPrice || limitPrice !== '');

  // Display-only estimate: Number math is fine here, it is never sent to the server.
  const estPrice = type === 'Limit' ? (priceError ? null : limitPrice) : lastPrice;
  const estimate =
    !qtyError && estPrice
      ? (Number(quantity) * Number(estPrice)).toLocaleString('en-US', {
          minimumFractionDigits: 2,
          maximumFractionDigits: 2,
        })
      : null;

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!isValid || submitting) return;
    setSubmitting(true);
    setOutcome(null);
    try {
      const result = await placeOrder(accountId, {
        symbol,
        side,
        type,
        quantity,
        ...(type === 'Limit' ? { limitPrice } : {}),
      });
      setOutcome({ kind: 'order', ...result });
      onPlaced(result.order);
    } catch (err) {
      setOutcome({ kind: 'error', message: errorMessage(err, t) });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="card" aria-labelledby="ticket-title">
      <div className="card-head">
        <h2 id="ticket-title">{t('orderTicket')}</h2>
      </div>

      <form className="ticket" onSubmit={handleSubmit} noValidate>
        <div className="field">
          <label htmlFor="ticket-symbol">{t('symbol')}</label>
          <select id="ticket-symbol" value={symbol} onChange={(e) => onSymbolChange(e.target.value)}>
            {instruments.map((i) => (
              <option key={i.symbol} value={i.symbol}>
                {displaySymbol(i.symbol, i)}
              </option>
            ))}
          </select>
        </div>

        <div className="field-row">
          <fieldset className="field">
            <legend>{t('side')}</legend>
            <div className="segmented">
              {SIDES.map((s) => (
                <button
                  key={s}
                  type="button"
                  aria-pressed={side === s}
                  className={side === s ? `active side-${s.toLowerCase()}` : ''}
                  onClick={() => setSide(s)}
                >
                  {t(`side.${s}`)}
                </button>
              ))}
            </div>
          </fieldset>

          <fieldset className="field">
            <legend>{t('type')}</legend>
            <div className="segmented">
              {TYPES.map((ty) => (
                <button
                  key={ty}
                  type="button"
                  aria-pressed={type === ty}
                  className={type === ty ? 'active' : ''}
                  onClick={() => setType(ty)}
                >
                  {t(`type.${ty}`)}
                </button>
              ))}
            </div>
          </fieldset>
        </div>

        <div className="field">
          <label htmlFor="ticket-qty">
            {t('quantity')} <span className="hint">({t('step', { step })})</span>
          </label>
          <div className="input-affix">
            <input
              id="ticket-qty"
              type="text"
              inputMode="decimal"
              autoComplete="off"
              dir="ltr"
              placeholder={step}
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
              onBlur={() => setTouched((v) => ({ ...v, quantity: true }))}
              aria-invalid={!!showQtyError}
              aria-describedby={showQtyError ? 'ticket-qty-err' : undefined}
            />
            <span className="affix" dir="ltr">
              {asset}
            </span>
          </div>
          {showQtyError && (
            <p id="ticket-qty-err" className="field-error">
              {t(qtyError, { step, tick })}
            </p>
          )}
        </div>

        {type === 'Limit' && (
          <div className="field">
            <label htmlFor="ticket-price">
              {t('limitPrice')} <span className="hint">({t('tick', { tick })})</span>
            </label>
            <div className="input-affix">
              <input
                id="ticket-price"
                type="text"
                inputMode="decimal"
                autoComplete="off"
                dir="ltr"
                placeholder={lastPrice ? formatPrice(lastPrice, instrument).replace(/,/g, '') : tick}
                value={limitPrice}
                onChange={(e) => setLimitPrice(e.target.value)}
                onBlur={() => setTouched((v) => ({ ...v, limitPrice: true }))}
                aria-invalid={!!showPriceError}
                aria-describedby={showPriceError ? 'ticket-price-err' : undefined}
              />
              <span className="affix" dir="ltr">
                {instrument?.quoteAsset ?? ''}
              </span>
            </div>
            {showPriceError && (
              <p id="ticket-price-err" className="field-error">
                {t(priceError, { step, tick })}
              </p>
            )}
          </div>
        )}

        <dl className="summary">
          <div>
            <dt>
              {t('estimatedValue')} <span className="hint">({t('estimateNote')})</span>
            </dt>
            <dd>
              {estimate ? (
                <Num text={`≈ ${estimate} ${instrument?.quoteAsset ?? ''}`} />
              ) : (
                <span className="muted">—</span>
              )}
            </dd>
          </div>
          <div>
            <dt>{t('availableCash')}</dt>
            <dd>
              <Num text={availableCash ? `${formatMoney(availableCash)} USDT` : '—'} raw={availableCash} />
            </dd>
          </div>
        </dl>

        <button
          type="submit"
          className={`btn btn-block ${side === 'Buy' ? 'btn-buy' : 'btn-sell'}`}
          disabled={!isValid || submitting}
          aria-busy={submitting}
        >
          {submitting ? t('submitting') : t(side === 'Buy' ? 'submitBuy' : 'submitSell', { asset })}
        </button>

        <div aria-live="polite">{outcome && <OutcomeMessage outcome={outcome} instruments={instruments} />}</div>
      </form>
    </section>
  );
}

function OutcomeMessage({ outcome, instruments }: { outcome: Outcome; instruments: Instrument[] }) {
  const { t } = useI18n();
  if (outcome.kind === 'error') {
    return <p className="alert alert-error">{t('result.error', { message: outcome.message })}</p>;
  }

  const { order, replayed } = outcome;
  const inst = instruments.find((i) => i.symbol === order.symbol);
  let text: string;
  let cls: string;
  switch (order.status) {
    case 'Filled':
      text = t('result.filled', {
        side: t(`side.${order.side}`),
        qty: formatQty(order.quantity, inst),
        asset: baseAsset(order.symbol, inst),
        price: formatPrice(order.fillPrice, inst),
      });
      cls = 'alert-success';
      break;
    case 'Open':
      text = t('result.open', { price: formatPrice(order.limitPrice, inst) });
      cls = 'alert-info';
      break;
    case 'Rejected': {
      const key = order.rejectReason ? reasonKey(order.rejectReason) : null;
      text = t('result.rejected', { reason: key ? t(key) : (order.rejectReason ?? '') });
      cls = 'alert-error';
      break;
    }
    default:
      text = t('result.cancelled');
      cls = 'alert-info';
  }

  return (
    <div className={`alert ${cls}`}>
      <p>{text}</p>
      {replayed && <p className="small">{t('result.replayed')}</p>}
    </div>
  );
}
