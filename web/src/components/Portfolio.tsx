import type { Instrument, Portfolio as PortfolioData } from '../api';
import { sign } from '../decimal';
import { displaySymbol, formatMoney, formatPrice, formatQty } from '../format';
import { useI18n, type MessageKey } from '../i18n';
import { Num } from './Num';

interface PortfolioProps {
  portfolio: PortfolioData | null;
  instruments: Instrument[];
}

export function Portfolio({ portfolio, instruments }: PortfolioProps) {
  const { t } = useI18n();

  if (!portfolio) return <p className="muted pad">{t('loading')}</p>;

  const stats: { key: MessageKey; value: string; pnl?: boolean }[] = [
    { key: 'equity', value: portfolio.equity },
    { key: 'cash', value: portfolio.cash },
    { key: 'reserved', value: portfolio.reservedCash },
    { key: 'available', value: portfolio.availableCash },
    { key: 'realizedPnl', value: portfolio.realizedPnl, pnl: true },
    { key: 'unrealizedPnl', value: portfolio.unrealizedPnl, pnl: true },
  ];

  // Hide fully closed positions unless they still carry realized P&L.
  const positions = portfolio.positions.filter((p) => sign(p.quantity) !== 0 || sign(p.realizedPnl) !== 0);

  return (
    <div>
      <dl className="stats">
        {stats.map((s) => (
          <div key={s.key} className="stat">
            <dt>{t(s.key)}</dt>
            <dd>
              <Num text={formatMoney(s.value, s.pnl)} raw={s.value} pnl={s.pnl ? s.value : undefined} />
            </dd>
          </div>
        ))}
      </dl>

      <h3 className="section-title">{t('positions')}</h3>
      {positions.length === 0 ? (
        <p className="empty">{t('noPositions')}</p>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th scope="col">{t('symbol')}</th>
                <th scope="col" className="end">{t('qty')}</th>
                <th scope="col" className="end">{t('avgCost')}</th>
                <th scope="col" className="end">{t('marketPrice')}</th>
                <th scope="col" className="end">{t('value')}</th>
                <th scope="col" className="end">{t('unrealizedPnl')}</th>
                <th scope="col" className="end">{t('realizedPnl')}</th>
              </tr>
            </thead>
            <tbody>
              {positions.map((p) => {
                const inst = instruments.find((i) => i.symbol === p.symbol);
                return (
                  <tr key={p.symbol}>
                    <td>
                      <span dir="ltr" className="sym">
                        {displaySymbol(p.symbol, inst)}
                      </span>
                    </td>
                    <td className="end">
                      <Num text={formatQty(p.quantity, inst)} raw={p.quantity} />
                    </td>
                    <td className="end">
                      <Num text={formatPrice(p.averageCost, inst)} raw={p.averageCost} />
                    </td>
                    <td className="end">
                      <Num text={formatPrice(p.marketPrice, inst)} raw={p.marketPrice} />
                    </td>
                    <td className="end">
                      <Num text={formatMoney(p.marketValue)} raw={p.marketValue} />
                    </td>
                    <td className="end">
                      <Num text={formatMoney(p.unrealizedPnl, true)} raw={p.unrealizedPnl} pnl={p.unrealizedPnl} />
                    </td>
                    <td className="end">
                      <Num text={formatMoney(p.realizedPnl, true)} raw={p.realizedPnl} pnl={p.realizedPnl} />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
