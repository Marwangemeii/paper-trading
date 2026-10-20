import { useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { API_URL, type Order, type PriceTick } from './api';

export type HubStatus = 'connecting' | 'live' | 'reconnecting' | 'offline';

interface Handlers {
  onPrice: (tick: PriceTick) => void;
  onOrderUpdated: (order: Order) => void;
}

const RESTART_DELAY_MS = 5000;

/**
 * Connects to the market hub, joins the account's group and forwards events.
 * withAutomaticReconnect() handles drops after a successful connect; if the
 * first connect fails (or retries give up) we keep trying every few seconds.
 */
export function useMarketHub(accountId: string | null, handlers: Handlers): HubStatus {
  const [status, setStatus] = useState<HubStatus>('connecting');
  const handlersRef = useRef(handlers);

  useEffect(() => {
    handlersRef.current = handlers;
  });

  useEffect(() => {
    if (!accountId) return;

    let disposed = false;
    let restartTimer: ReturnType<typeof setTimeout> | undefined;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/market`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('price', (tick: PriceTick) => handlersRef.current.onPrice(tick));
    connection.on('orderUpdated', (order: Order) => handlersRef.current.onOrderUpdated(order));

    const join = () => connection.invoke('JoinAccount', accountId);

    connection.onreconnecting(() => !disposed && setStatus('reconnecting'));
    connection.onreconnected(async () => {
      if (disposed) return;
      try {
        await join();
      } catch {
        // Prices still flow; order updates are also covered by polling.
      }
      setStatus('live');
    });
    connection.onclose(() => {
      if (disposed) return;
      setStatus('offline');
      scheduleRestart();
    });

    async function start() {
      if (disposed) return;
      try {
        await connection.start();
      } catch {
        if (disposed) return;
        setStatus('offline');
        if (connection.state === HubConnectionState.Disconnected) scheduleRestart();
        return;
      }
      if (disposed) return;
      try {
        await join();
      } catch {
        // Prices still flow; order updates are also covered by polling.
      }
      setStatus('live');
    }

    function scheduleRestart() {
      clearTimeout(restartTimer);
      restartTimer = setTimeout(start, RESTART_DELAY_MS);
    }

    void start();

    return () => {
      disposed = true;
      clearTimeout(restartTimer);
      void connection.stop();
    };
  }, [accountId]);

  return status;
}
