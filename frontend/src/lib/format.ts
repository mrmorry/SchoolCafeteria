// Currency is configurable per school (SystemSetting "currency.default") — never hardcode a
// symbol. The dashboard reads it once via useSettings() and formatCurrency takes it explicitly;
// this fallback only covers the brief moment before that setting has loaded.
const FALLBACK_CURRENCY = 'USD';
const LOCALE = 'es-PA';

export function formatCurrency(amount: number, currency = FALLBACK_CURRENCY): string {
  return new Intl.NumberFormat(LOCALE, { style: 'currency', currency }).format(amount);
}

export function formatDateTime(isoUtc: string): string {
  return new Intl.DateTimeFormat(LOCALE, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(isoUtc));
}

export function formatDate(isoUtc: string): string {
  return new Intl.DateTimeFormat(LOCALE, { dateStyle: 'medium' }).format(new Date(isoUtc));
}
