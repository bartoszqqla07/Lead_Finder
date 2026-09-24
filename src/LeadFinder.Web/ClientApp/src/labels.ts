import type { Lead, LeadStatus, OutreachStage, ScoreTier, SearchRunState } from './types';

/** Grupy statusów w kolejności priorytetu (jak sortowanie w CSV). */
export type StatusGroup = 'hot' | 'wordpress' | 'other';

export const statusGroupOf = (status: LeadStatus): StatusGroup =>
  status === 'NoWebsite' || status === 'WebsiteDown' ? 'hot' : status === 'WordPress' ? 'wordpress' : 'other';

export const statusBadge: Record<LeadStatus, { label: string; tone: 'hot' | 'warm' | 'neutral' }> = {
  NoWebsite: { label: 'Brak strony', tone: 'hot' },
  WebsiteDown: { label: 'Strona nie działa', tone: 'hot' },
  WordPress: { label: 'WordPress', tone: 'warm' },
  HasWebsite: { label: 'Ma stronę', tone: 'neutral' },
};

export const scoreTierLabel: Record<ScoreTier, string> = {
  High: 'Wysoka szansa',
  Medium: 'Średnia szansa',
  Low: 'Niska szansa',
};

export const stages: { value: OutreachStage; label: string }[] = [
  { value: 'New', label: 'Nowy' },
  { value: 'Later', label: 'Na później' },
  { value: 'Contacted', label: 'Skontaktowany' },
  { value: 'Replied', label: 'Odpowiedział' },
  { value: 'Client', label: 'Klient' },
  { value: 'Rejected', label: 'Odpada' },
];

export const stageLabel = (stage: OutreachStage) => stages.find((s) => s.value === stage)?.label ?? stage;

export const runStateLabel: Record<SearchRunState, string> = {
  Running: 'w toku',
  Completed: 'zakończone',
  Failed: 'błąd',
  Cancelled: 'przerwane',
};

const dateFormat = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'short' });
const dateTimeFormat = new Intl.DateTimeFormat('pl-PL', {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
});
const ratingFormat = new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

export const formatDate = (iso: string) => dateFormat.format(new Date(iso));
export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso));
export const formatRating = (rating: number) => ratingFormat.format(rating);

/** Host strony bez "www." – do wyświetlania w tabeli. */
export function websiteHost(lead: Lead): string | null {
  if (!lead.websiteUri) return null;
  try {
    return new URL(lead.websiteUri).hostname.replace(/^www\./, '');
  } catch {
    return lead.websiteUri;
  }
}

/** Polska odmiana liczebników: 1 lead, 2–4 leady, 5+ leadów. */
export function plural(count: number, one: string, few: string, many: string): string {
  if (count === 1) return one;
  const lastDigit = count % 10;
  const lastTwo = count % 100;
  return lastDigit >= 2 && lastDigit <= 4 && (lastTwo < 12 || lastTwo > 14) ? few : many;
}
