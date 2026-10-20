import type { Instrument } from '../api';
import { displaySymbol, formatPrice } from '../format';
import { useI18n } from '../i18n';
import { Num } from './Num';

export interface LivePrice {
  price: string | null;
  /** Direction of the last change: 1 up, -1 down, 0 unchanged/unknown. */
  dir: -1 | 0 | 1;
  /** Increments on every tick so the flash animation restarts. */
  seq: number;
}

interface PriceTableProps {
  instruments: Instrument[];
  prices: Record<string, LivePrice>;
  selected: string;
  onSelect: (symbol: string) => void;
}

export function PriceTable({ instruments, prices, selected, onSelect }: PriceTableProps) {
  const { t } = useI18n();

  return (
    <section className="card" aria-labelledby="markets-title">
      <div className="card-head">
        <h2 id="markets-title">{t('markets')}</h2>
        <span className="muted small">{t('selectMarketHint')}</span>
      </div>
      <div className="table-wrap">
        <table className="table price-table">
          <thead>
            <tr>
              <th scope="col">{t('symbol')}</th>
              <th scope="col" className="end">
                {t('lastPrice')}
              </th>
              <th scope="col" className="center">
                {t('change')}
              </th>
            </tr>
          </thead>
          <tbody>
            {instruments.map((inst) => {
              const live = prices[inst.symbol];
              const price = live?.price ?? inst.lastPrice;
              const dir = live?.dir ?? 0;
              const isSelected = inst.symbol === selected;
              return (
                <tr
                  key={inst.symbol}
                  className={isSelected ? 'selected clickable' : 'clickable'}
                  onClick={() => onSelect(inst.symbol)}
                >
                  <td>
                    <button
                      type="button"
                      className="link-btn"
                      aria-pressed={isSelected}
                      onClick={(e) => {
                        e.stopPropagation();
                        onSelect(inst.symbol);
                      }}
                    >
                      <span dir="ltr" className="sym">
                        {displaySymbol(inst.symbol, inst)}
                      </span>
                    </button>
                  </td>
                  <td className="end">
                    {price ? (
                      <span
                        key={live?.seq ?? 0}
                        className={`price-cell ${dir > 0 ? 'flash-up' : dir < 0 ? 'flash-down' : ''}`}
                      >
                        <Num text={formatPrice(price, inst)} raw={price} />
                      </span>
                    ) : (
                      <span className="muted">{t('noPrice')}</span>
                    )}
                  </td>
                  <td className="center">
                    {dir > 0 ? (
                      <span className="tick pos" role="img" aria-label={t('up')}>
                        ▲
                      </span>
                    ) : dir < 0 ? (
                      <span className="tick neg" role="img" aria-label={t('down')}>
                        ▼
                      </span>
                    ) : (
                      <span className="tick muted" aria-hidden="true">
                        –
                      </span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}
