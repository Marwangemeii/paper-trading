import { ApiError } from './api';
import type { Translate } from './i18n';

/** Human-readable message for a failed API call (server detail or translated network error). */
export function errorMessage(err: unknown, t: Translate): string {
  if (err instanceof ApiError) {
    if (err.isNetwork) return t('networkError');
    return err.detail || err.title || `HTTP ${err.status}`;
  }
  return err instanceof Error ? err.message : String(err);
}
