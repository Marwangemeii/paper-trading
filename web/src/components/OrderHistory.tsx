import { useState } from 'react';
import { cancelOrder, type Instrument, type Order } from '../api';
import { errorMessage } from '../errors';
import { displaySymbol, formatMoney, formatPrice, formatQty } from '../format';
import { reasonKey, useI18n } from '../i18n';
import { Num } from './Num';

interface OrderHistoryProps {
  accountId: string;
  orders: Order[] | null;
  instruments: Instrument[];
  onChanged: () => void;
}

export function OrderHistory({ accountId, orders, instruments, onChanged }: OrderHistoryProps) {
  const { t, dateLocale } = useI18n();
  const [cancelling, setCancelling] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!orders) return <p className="muted pad">{t('loading')}</p>;
  if (orders.length === 0) return <p className="empty">{t('noOrders')}</p>;

  async function handleCancel(orderId: string) {
    setCancelling(orderId);
    setError(null);
    try {
      await cancelOrder(accountId, orderId);
    } catch (err) {
      setError(errorMessage(err, t));
    } finally {
      setCancelling(null);
      onChanged();
    }
  }

  const timeFormat = new Intl.DateTimeFormat(dateLocale, { dateStyle: 'short', timeStyle: 'medium' });

  return (
    <div>
      {error && (
        <p className="alert alert-error" role="alert">
          {t('result.error', { message: error })}
        </p>
      )}
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              <th scope="col">{t('time')}</th>
              <th scope="col">{t('symbol')}</th>
              <th scope="col">{t('side')}</th>
              <th scope="col">{t('type')}</th>
              <th scope="col" className="end">{t('qty')}</th>
              <th scope="col" className="end">{t('limit')}</th>
              <th scope="col">{t('status')}</th>
              <th scope="col" className="end">{t('fillPrice')}</th>
              <th scope="col" className="end">{t('value')}</th>
              <th scope="col">{t('reason')}</th>
              <th scope="col">
                <span className="visually-hidden">{t('actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {orders.map((o) => {
              const inst = instruments.find((i) => i.symbol === o.symbol);
              const key = o.rejectReason ? reasonKey(o.rejectReason) : null;
              return (
                <tr key={o.id}>
                  <td className="nowrap">
                    <Num text={timeFormat.format(new Date(o.createdAt))} raw={o.createdAt} />
                  </td>
                  <td>
                    <span dir="ltr" className="sym">
                      {displaySymbol(o.symbol, inst)}
                    </span>
                  </td>
                  <td className={o.side === 'Buy' ? 'pos' : 'neg'}>{t(`side.${o.side}`)}</td>
                  <td>{t(`type.${o.type}`)}</td>
                  <td className="end">
                    <Num text={formatQty(o.quantity, inst)} raw={o.quantity} />
                  </td>
                  <td className="end">
                    {o.limitPrice ? <Num text={formatPrice(o.limitPrice, inst)} raw={o.limitPrice} /> : '—'}
                  </td>
                  <td>
                    <span className={`badge badge-${o.status.toLowerCase()}`}>{t(`status.${o.status}`)}</span>
                  </td>
                  <td className="end">
                    {o.fillPrice ? <Num text={formatPrice(o.fillPrice, inst)} raw={o.fillPrice} /> : '—'}
                  </td>
                  <td className="end">{o.notional ? <Num text={formatMoney(o.notional)} raw={o.notional} /> : '—'}</td>
                  <td className="reason">{key ? t(key) : (o.rejectReason ?? '')}</td>
                  <td>
                    {o.status === 'Open' && (
                      <button
                        type="button"
                        className="btn btn-sm btn-outline"
                        onClick={() => handleCancel(o.id)}
                        disabled={cancelling === o.id}
                      >
                        {cancelling === o.id ? t('cancelling') : t('cancel')}
                      </button>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
