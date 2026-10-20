import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { I18nContext, makeTranslate, type I18nValue, type Lang } from './i18n';

const STORAGE_KEY = 'pt.lang';

function readStoredLang(): Lang {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'ar' ? 'ar' : 'en';
  } catch {
    return 'en';
  }
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLang] = useState<Lang>(readStoredLang);

  useEffect(() => {
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Storage may be unavailable (private mode); the toggle still works for this session.
    }
  }, [lang]);

  const value = useMemo<I18nValue>(
    () => ({
      lang,
      setLang,
      t: makeTranslate(lang),
      dateLocale: lang === 'ar' ? 'ar-u-nu-latn' : 'en-GB',
    }),
    [lang],
  );

  useEffect(() => {
    document.title = value.t('appTitle');
  }, [value]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}
