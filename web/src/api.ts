// Typed client for the Paper Trading API.
// All money/quantity values stay as strings end to end (exact decimals).

export const API_URL: string = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/+$/, '') || 'http://localhost:5080';

export type Side = 'Buy' | 'Sell';
export type OrderType = 'Market' | 'Limit';
export type OrderStatus = 'Open' | 'Filled' | 'Cancelled' | 'Rejected';
export type RejectReason = 'NO_MARKET_PRICE' | 'INSUFFICIENT_FUNDS' | 'INSUFFICIENT_POSITION';

export interface Instrument {
  symbol: string;
  baseAsset: string;
  quoteAsset: string;
  quantityStep: string;
  tickSize: string;
  lastPrice: string | null;
}

export interface Position {
  symbol: string;
  quantity: string;
  reservedQuantity: string;
  averageCost: string;
  costBasis: string;
  marketPrice: string | null;
  marketValue: string;
  unrealizedPnl: string;
  realizedPnl: string;
}

export interface Portfolio {
  accountId: string;
  initialCash: string;
  cash: string;
  reservedCash: string;
  availableCash: string;
  marketValue: string;
  unrealizedPnl: string;
  realizedPnl: string;
  equity: string;
  positions: Position[];
}

export interface Order {
  id: string;
  accountId: string;
  symbol: string;
  side: Side;
  type: OrderType;
  quantity: string;
  limitPrice: string | null;
  status: OrderStatus;
  fillPrice: string | null;
  notional: string | null;
  rejectReason: RejectReason | string | null;
  createdAt: string;
  closedAt: string | null;
}

export interface PlaceOrderRequest {
  symbol: string;
  side: Side;
  type: OrderType;
  quantity: string;
  limitPrice?: string;
}

export interface PriceTick {
  symbol: string;
  price: string;
  at: string;
}

/** Error carrying the server's ProblemDetails (or a network failure). */
export class ApiError extends Error {
  readonly status: number; // 0 = network error
  readonly title?: string;
  readonly detail?: string;

  constructor(status: number, title?: string, detail?: string) {
    super(detail || title || (status === 0 ? 'Network error' : `HTTP ${status}`));
    this.name = 'ApiError';
    this.status = status;
    this.title = title;
    this.detail = detail;
  }

  get isNetwork(): boolean {
    return this.status === 0;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<{ data: T; response: Response }> {
  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      ...init,
      headers: { Accept: 'application/json', ...init?.headers },
    });
  } catch {
    throw new ApiError(0);
  }

  if (!response.ok) {
    let title: string | undefined;
    let detail: string | undefined;
    try {
      const problem = (await response.json()) as { title?: string; detail?: string };
      title = problem.title;
      detail = problem.detail;
    } catch {
      // Body was not JSON; fall back to the status code.
    }
    throw new ApiError(response.status, title, detail);
  }

  const text = await response.text();
  return { data: (text ? JSON.parse(text) : undefined) as T, response };
}

export async function getInstruments(): Promise<Instrument[]> {
  return (await request<Instrument[]>('/api/instruments')).data;
}

export async function createAccount(): Promise<{ accountId: string; startingCash: string }> {
  return (await request<{ accountId: string; startingCash: string }>('/api/accounts', { method: 'POST' })).data;
}

export async function getPortfolio(accountId: string): Promise<Portfolio> {
  return (await request<Portfolio>(`/api/accounts/${encodeURIComponent(accountId)}/portfolio`)).data;
}

export async function getOrders(accountId: string): Promise<Order[]> {
  return (await request<Order[]>(`/api/accounts/${encodeURIComponent(accountId)}/orders`)).data;
}

export async function cancelOrder(accountId: string, orderId: string): Promise<Order> {
  return (
    await request<Order>(`/api/accounts/${encodeURIComponent(accountId)}/orders/${encodeURIComponent(orderId)}`, {
      method: 'DELETE',
    })
  ).data;
}

export interface PlaceOrderResult {
  order: Order;
  /** True when the server returned the original order for a repeated Idempotency-Key. */
  replayed: boolean;
}

export async function placeOrder(accountId: string, body: PlaceOrderRequest): Promise<PlaceOrderResult> {
  // One key per user submission. It is reused for the retry below.
  const idempotencyKey = crypto.randomUUID();
  const send = async (): Promise<PlaceOrderResult> => {
    const { data, response } = await request<Order>(`/api/accounts/${encodeURIComponent(accountId)}/orders`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify(body),
    });
    return {
      order: data,
      replayed: response.status === 200 || response.headers.get('Idempotent-Replayed') === 'true',
    };
  };

  try {
    return await send();
  } catch (err) {
    // A network error means we don't know if the server got the order.
    // Retrying with the SAME Idempotency-Key is safe: the server returns the
    // original order instead of placing a second one.
    if (err instanceof ApiError && err.isNetwork) return await send();
    throw err;
  }
}
