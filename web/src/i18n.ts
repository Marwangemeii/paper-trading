import { createContext, useContext } from 'react';

export type Lang = 'en' | 'ar';

const en = {
  appTitle: 'Paper Trading',
  'conn.live': 'Live',
  'conn.connecting': 'Connecting…',
  'conn.reconnecting': 'Reconnecting…',
  'conn.offline': 'Offline',
  resetAccount: 'Reset account',
  resetConfirm: 'Start over with a new account? Your current balance and orders will no longer be shown.',
  language: 'Language',
  loading: 'Loading…',
  serverUnreachable: 'Cannot reach the server. Check that the API is running.',
  retry: 'Retry',

  markets: 'Markets',
  symbol: 'Symbol',
  lastPrice: 'Last price',
  change: 'Tick',
  up: 'Up',
  down: 'Down',
  noPrice: 'No price yet',
  selectMarketHint: 'Select a row to trade it.',

  orderTicket: 'Order ticket',
  side: 'Side',
  type: 'Type',
  quantity: 'Quantity',
  limitPrice: 'Limit price',
  step: 'Step {step}',
  tick: 'Tick {tick}',
  estimatedValue: 'Estimated value',
  estimateNote: 'estimate only',
  availableCash: 'Available cash',
  available: 'Available',
  submitBuy: 'Buy {asset}',
  submitSell: 'Sell {asset}',
  submitting: 'Sending…',
  'err.required': 'This field is required.',
  'err.format': 'Enter a plain number, e.g. 0.5',
  'err.positive': 'Must be greater than 0.',
  'err.qtyStep': 'Must be a multiple of {step}.',
  'err.priceTick': 'Must be a multiple of {tick}.',
  'result.filled': 'Filled: {side} {qty} {asset} at {price}.',
  'result.open': 'Order placed. Waiting for the price to reach {price}.',
  'result.rejected': 'Rejected: {reason}',
  'result.cancelled': 'Order cancelled.',
  'result.replayed': 'Duplicate request detected; showing the original order.',
  'result.error': 'Error: {message}',
  networkError: 'Network error. Please try again.',

  portfolio: 'Portfolio',
  cash: 'Cash',
  reserved: 'Reserved',
  equity: 'Equity',
  marketValue: 'Market value',
  realizedPnl: 'Realized P&L',
  unrealizedPnl: 'Unrealized P&L',
  positions: 'Positions',
  avgCost: 'Avg. cost',
  marketPrice: 'Market price',
  value: 'Value',
  noPositions: 'No positions yet. Place an order to get started.',

  orderHistory: 'Orders',
  time: 'Time',
  qty: 'Qty',
  limit: 'Limit',
  status: 'Status',
  fillPrice: 'Fill price',
  reason: 'Reason',
  actions: 'Actions',
  cancel: 'Cancel',
  cancelling: 'Cancelling…',
  noOrders: 'No orders yet.',

  'side.Buy': 'Buy',
  'side.Sell': 'Sell',
  'type.Market': 'Market',
  'type.Limit': 'Limit',
  'status.Open': 'Open',
  'status.Filled': 'Filled',
  'status.Cancelled': 'Cancelled',
  'status.Rejected': 'Rejected',
  'reason.NO_MARKET_PRICE': 'No market price available yet.',
  'reason.INSUFFICIENT_FUNDS': 'Not enough available cash.',
  'reason.INSUFFICIENT_POSITION': 'Not enough of this asset to sell.',
} as const;

export type MessageKey = keyof typeof en;
type Dictionary = Record<MessageKey, string>;

const ar: Dictionary = {
  appTitle: 'التداول التجريبي',
  'conn.live': 'مباشر',
  'conn.connecting': 'جارٍ الاتصال…',
  'conn.reconnecting': 'جارٍ إعادة الاتصال…',
  'conn.offline': 'غير متصل',
  resetAccount: 'إعادة تعيين الحساب',
  resetConfirm: 'هل تريد البدء بحساب جديد؟ لن يظهر رصيدك وأوامرك الحالية بعد الآن.',
  language: 'اللغة',
  loading: 'جارٍ التحميل…',
  serverUnreachable: 'تعذّر الوصول إلى الخادم. تأكد من تشغيل واجهة البرمجة.',
  retry: 'إعادة المحاولة',

  markets: 'الأسواق',
  symbol: 'الرمز',
  lastPrice: 'آخر سعر',
  change: 'الاتجاه',
  up: 'صعود',
  down: 'هبوط',
  noPrice: 'لا يوجد سعر بعد',
  selectMarketHint: 'اختر صفًا للتداول عليه.',

  orderTicket: 'تذكرة الأمر',
  side: 'الاتجاه',
  type: 'النوع',
  quantity: 'الكمية',
  limitPrice: 'السعر المحدد',
  step: 'الخطوة {step}',
  tick: 'وحدة السعر {tick}',
  estimatedValue: 'القيمة التقديرية',
  estimateNote: 'تقدير فقط',
  availableCash: 'النقد المتاح',
  available: 'المتاح',
  submitBuy: 'شراء {asset}',
  submitSell: 'بيع {asset}',
  submitting: 'جارٍ الإرسال…',
  'err.required': 'هذا الحقل مطلوب.',
  'err.format': 'أدخل رقمًا صحيحًا، مثل 0.5',
  'err.positive': 'يجب أن تكون القيمة أكبر من 0.',
  'err.qtyStep': 'يجب أن تكون من مضاعفات {step}.',
  'err.priceTick': 'يجب أن يكون من مضاعفات {tick}.',
  'result.filled': 'تم التنفيذ: {side} {qty} {asset} بسعر {price}.',
  'result.open': 'تم وضع الأمر. بانتظار وصول السعر إلى {price}.',
  'result.rejected': 'مرفوض: {reason}',
  'result.cancelled': 'تم إلغاء الأمر.',
  'result.replayed': 'تم اكتشاف طلب مكرر؛ يتم عرض الأمر الأصلي.',
  'result.error': 'خطأ: {message}',
  networkError: 'خطأ في الشبكة. يرجى المحاولة مرة أخرى.',

  portfolio: 'المحفظة',
  cash: 'النقد',
  reserved: 'المحجوز',
  equity: 'إجمالي القيمة',
  marketValue: 'القيمة السوقية',
  realizedPnl: 'الربح/الخسارة المحققة',
  unrealizedPnl: 'الربح/الخسارة غير المحققة',
  positions: 'المراكز',
  avgCost: 'متوسط التكلفة',
  marketPrice: 'سعر السوق',
  value: 'القيمة',
  noPositions: 'لا توجد مراكز بعد. ضع أمرًا للبدء.',

  orderHistory: 'الأوامر',
  time: 'الوقت',
  qty: 'الكمية',
  limit: 'الحد',
  status: 'الحالة',
  fillPrice: 'سعر التنفيذ',
  reason: 'السبب',
  actions: 'إجراءات',
  cancel: 'إلغاء',
  cancelling: 'جارٍ الإلغاء…',
  noOrders: 'لا توجد أوامر بعد.',

  'side.Buy': 'شراء',
  'side.Sell': 'بيع',
  'type.Market': 'سوق',
  'type.Limit': 'محدد',
  'status.Open': 'مفتوح',
  'status.Filled': 'منفّذ',
  'status.Cancelled': 'ملغى',
  'status.Rejected': 'مرفوض',
  'reason.NO_MARKET_PRICE': 'لا يتوفر سعر سوق بعد.',
  'reason.INSUFFICIENT_FUNDS': 'النقد المتاح غير كافٍ.',
  'reason.INSUFFICIENT_POSITION': 'لا تملك كمية كافية من هذا الأصل للبيع.',
};

export const dictionaries: Record<Lang, Dictionary> = { en, ar };

const FSI = String.fromCharCode(0x2068); // first-strong isolate
const PDI = String.fromCharCode(0x2069); // pop directional isolate

export type Translate = (key: MessageKey, vars?: Record<string, string>) => string;

export function makeTranslate(lang: Lang): Translate {
  const dict = dictionaries[lang];
  return (key, vars) => {
    let text = dict[key] ?? en[key] ?? key;
    if (vars) {
      // Wrap values in Unicode isolates (FSI…PDI) so numbers/symbols keep LTR order inside Arabic text.
      for (const [name, value] of Object.entries(vars)) text = text.split(`{${name}}`).join(FSI + value + PDI);
    }
    return text;
  };
}

/** Translates a server reject reason code, falling back to the raw code. */
export function reasonKey(code: string): MessageKey | null {
  const key = `reason.${code}`;
  return key in en ? (key as MessageKey) : null;
}

export interface I18nValue {
  lang: Lang;
  setLang: (lang: Lang) => void;
  t: Translate;
  /** Locale for dates: Arabic text with Latin digits. */
  dateLocale: string;
}

export const I18nContext = createContext<I18nValue>({
  lang: 'en',
  setLang: () => {},
  t: makeTranslate('en'),
  dateLocale: 'en-GB',
});

export function useI18n(): I18nValue {
  return useContext(I18nContext);
}
