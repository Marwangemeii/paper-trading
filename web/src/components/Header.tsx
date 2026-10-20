import { useI18n, type Lang } from '../i18n';
import type { HubStatus } from '../useMarketHub';

interface HeaderProps {
  status: HubStatus;
  onReset: () => void;
  resetDisabled: boolean;
}

const LANGS: { code: Lang; label: string }[] = [
  { code: 'en', label: 'EN' },
  { code: 'ar', label: 'عربي' },
];

export function Header({ status, onReset, resetDisabled }: HeaderProps) {
  const { t, lang, setLang } = useI18n();

  return (
    <header className="header">
      <h1 className="brand">
        <span className="brand-mark" aria-hidden="true">
          ◆
        </span>
        {t('appTitle')}
      </h1>

      <div className="header-actions">
        <span className={`conn conn-${status}`} role="status" aria-live="polite">
          <span className="conn-dot" aria-hidden="true" />
          {t(`conn.${status}`)}
        </span>

        <div className="segmented segmented-sm" role="group" aria-label={t('language')}>
          {LANGS.map((l) => (
            <button
              key={l.code}
              type="button"
              lang={l.code}
              aria-pressed={lang === l.code}
              className={lang === l.code ? 'active' : ''}
              onClick={() => setLang(l.code)}
            >
              {l.label}
            </button>
          ))}
        </div>

        <button type="button" className="btn btn-ghost" onClick={onReset} disabled={resetDisabled}>
          {t('resetAccount')}
        </button>
      </div>
    </header>
  );
}
