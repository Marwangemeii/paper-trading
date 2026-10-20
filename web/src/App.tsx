import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  createAccount,
  getInstruments,
  getOrders,
  getPortfolio,
  type Instrument,
  type Order,
  type Portfolio as PortfolioData,
  type PriceTick,
} from './api';
import { Header } from './components/Header';
import { OrderHistory } from './components/OrderHistory';
import { OrderTicket } from './components/OrderTicket';
import { Portfolio } from './components/Portfolio';
import { PriceTable, type LivePrice } from './components/PriceTable';
import { compareDecimal } from './decimal';
import { useI18n } from './i18n';
import { useMarketHub } from './useMarketHub';

const ACCOUNT_KEY = 'pt.accountId';
const REFRESH_MS = 2000;

type Tab = 'portfolio' | 'orders';

function readStoredAccount(): string | null {
  try {
    return localStorage.getItem(ACCOUNT_KEY);
  } catch {
    return null;
  }
}

function storeAccount(id: string) {
  try {
    localStorage.setItem(ACCOUNT_KEY, id);
  } catch {
    // Not fatal: the account just won't survive a reload.
  }
}

async function newAccount(): Promise<string> {
  const { accountId } = await createAccount();
  storeAccount(accountId);
  return accountId;
}

export default function App() {
  const { t } = useI18n();
  const [instruments, setInstruments] = useState<Instrument[]>([]);
  const [prices, setPrices] = useState<Record<string, LivePrice>>({});
  const [selected, setSelected] = useState('BTCUSDT');
  const [accountId, setAccountId] = useState<string | null>(null);
  const [portfolio, setPortfolio] = useState<PortfolioData | null>(null);
  const [orders, setOrders] = useState<Order[] | null>(null);
  const [bootError, setBootError] = useState(false);
  const [bootAttempt, setBootAttempt] = useState(0);
  const [tab, setTab] = useState<Tab>('portfolio');
  const [resetting, setResetting] = useState(false);

  const accountRef = useRef(accountId);
  useEffect(() => {
    accountRef.current = accountId;
  }, [accountId]);

  // Bootstrap: instruments + account (reuse the stored one if the server still knows it).
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const list = await getInstruments();
        if (cancelled) return;
        setInstruments(list);
        setPrices(Object.fromEntries(list.map((i) => [i.symbol, { price: i.lastPrice, dir: 0 as const, seq: 0 }])));
        setSelected((s) => (list.some((i) => i.symbol === s) ? s : (list[0]?.symbol ?? s)));

        let id = readStoredAccount();
        if (id) {
          try {
            const p = await getPortfolio(id);
            if (cancelled) return;
            setPortfolio(p);
          } catch (err) {
            if (!(err instanceof ApiError && err.status === 404)) throw err;
            id = null;
          }
        }
        if (!id) id = await newAccount();
        if (cancelled) return;
        setAccountId(id);
        setBootError(false);
      } catch {
        if (!cancelled) setBootError(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [bootAttempt]);

  const refresh = useCallback(async () => {
    const id = accountRef.current;
    if (!id) return;
    const [p, o] = await Promise.allSettled([getPortfolio(id), getOrders(id)]);
    if (accountRef.current !== id) return; // account was reset meanwhile
    if (p.status === 'fulfilled') setPortfolio(p.value);
    if (o.status === 'fulfilled') setOrders(o.value);
    // The server no longer knows this account (e.g. it restarted): bootstrap a new one.
    if (p.status === 'rejected' && p.reason instanceof ApiError && p.reason.status === 404) {
      accountRef.current = null;
      setAccountId(null);
      setBootAttempt((n) => n + 1);
    }
  }, []);

  // Refresh on load / account change and every ~2s (unrealized P&L moves with price).
  useEffect(() => {
    if (!accountId) return;
    void refresh();
    const timer = setInterval(() => void refresh(), REFRESH_MS);
    return () => clearInterval(timer);
  }, [accountId, refresh]);

  const onPrice = useCallback((tick: PriceTick) => {
    setPrices((prev) => {
      const old = prev[tick.symbol];
      if (old?.price === tick.price) return prev;
      const dir = old?.price ? compareDecimal(tick.price, old.price) : 0;
      if (old && dir === 0) return prev; // same value, different formatting
      return { ...prev, [tick.symbol]: { price: tick.price, dir, seq: (old?.seq ?? 0) + 1 } };
    });
  }, []);

  const status = useMarketHub(accountId, { onPrice, onOrderUpdated: () => void refresh() });

  async function handleReset() {
    if (!window.confirm(t('resetConfirm'))) return;
    setResetting(true);
    try {
      const id = await newAccount();
      setPortfolio(null);
      setOrders(null);
      setAccountId(id);
      accountRef.current = id;
    } catch {
      setBootError(true);
    } finally {
      setResetting(false);
    }
  }

  const openCount = orders?.filter((o) => o.status === 'Open').length ?? 0;
  const tabs: { id: Tab; label: string }[] = [
    { id: 'portfolio', label: t('portfolio') },
    { id: 'orders', label: t('orderHistory') },
  ];

  return (
    <div className="app">
      <Header status={accountId ? status : 'connecting'} onReset={handleReset} resetDisabled={resetting || !accountId} />

      <main className="container">
        {bootError && (
          <div className="alert alert-error banner" role="alert">
            <span>{t('serverUnreachable')}</span>
            <button type="button" className="btn btn-sm btn-outline" onClick={() => setBootAttempt((n) => n + 1)}>
              {t('retry')}
            </button>
          </div>
        )}

        {!accountId ? (
          !bootError && <p className="muted pad">{t('loading')}</p>
        ) : (
          <>
            <div className="grid-top">
              <PriceTable instruments={instruments} prices={prices} selected={selected} onSelect={setSelected} />
              <OrderTicket
                accountId={accountId}
                instruments={instruments}
                symbol={selected}
                onSymbolChange={setSelected}
                lastPrice={prices[selected]?.price ?? null}
                availableCash={portfolio?.availableCash ?? null}
                onPlaced={() => void refresh()}
              />
            </div>

            <section className="card">
              <div className="tabs" role="tablist" aria-label={t('portfolio')}>
                {tabs.map((tb) => (
                  <button
                    key={tb.id}
                    type="button"
                    role="tab"
                    id={`tab-${tb.id}`}
                    aria-selected={tab === tb.id}
                    aria-controls={`panel-${tb.id}`}
                    className={tab === tb.id ? 'tab active' : 'tab'}
                    onClick={() => setTab(tb.id)}
                  >
                    {tb.label}
                    {tb.id === 'orders' && openCount > 0 && (
                      <span className="count" dir="ltr">
                        {openCount}
                      </span>
                    )}
                  </button>
                ))}
              </div>
              <div role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`} className="tab-panel">
                {tab === 'portfolio' ? (
                  <Portfolio portfolio={portfolio} instruments={instruments} />
                ) : (
                  <OrderHistory accountId={accountId} orders={orders} instruments={instruments} onChanged={() => void refresh()} />
                )}
              </div>
            </section>
          </>
        )}
      </main>
    </div>
  );
}
