import { statusGroupOf, type StatusGroup } from './labels';
import type { Lead, OutreachStage, ScoreTier } from './types';

/** Zakładki listy – każda odpowiada grupie etapów kontaktu. */
export type LeadView = 'new' | 'contact' | 'later' | 'client' | 'rejected' | 'all';

/** W której zakładce jest lead: zmiana etapu przenosi go między zakładkami. */
export const viewOfStage = (stage: OutreachStage): Exclude<LeadView, 'all'> => {
  switch (stage) {
    case 'New':
      return 'new';
    case 'Later':
      return 'later';
    case 'Client':
      return 'client';
    case 'Rejected':
      return 'rejected';
    default:
      return 'contact'; // Contacted, Replied
  }
};

export interface LeadFilters {
  /** Zakładka: "Nowe" to lista robocza, która maleje, gdy kontaktujesz się z salonami. */
  view: LeadView;
  query: string;
  /** Puste = wszystkie. */
  cities: string[];
  /** Puste = wszystkie. */
  categoryIds: string[];
  statusGroup: StatusGroup | 'all';
  scoreTier: ScoreTier | 'all';
  /** Tylko leady z przypomnieniem na dziś lub zaległym. */
  dueOnly: boolean;
  /** Tylko leady, które pojawiły się po raz pierwszy w tym wyszukiwaniu. */
  searchRunId: number | null;
}

export const defaultFilters: LeadFilters = {
  view: 'new',
  query: '',
  cities: [],
  categoryIds: [],
  statusGroup: 'all',
  scoreTier: 'all',
  dueOnly: false,
  searchRunId: null,
};

/** Data lokalna jako RRRR-MM-DD (format nextActionDate) – bez przesunięcia strefy czasowej. */
const toIsoDate = (date: Date) =>
  new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);

export const todayIso = () => toIsoDate(new Date());

/** Data za N dni jako RRRR-MM-DD. */
export const addDaysIso = (days: number) => {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return toIsoDate(date);
};

/** Przypomnienie na dziś albo zaległe (klienci i odrzuceni się nie liczą). */
export const isDue = (lead: Lead, today = todayIso()) =>
  lead.nextActionDate !== null && lead.nextActionDate <= today && lead.stage !== 'Client' && lead.stage !== 'Rejected';

export type SortKey = 'score' | 'priority' | 'name' | 'rating' | 'reviews' | 'firstSeen';
export interface SortState {
  key: SortKey;
  direction: 'asc' | 'desc';
}

/** Domyślnie: największa szansa na zlecenie na górze. */
export const defaultSort: SortState = { key: 'score', direction: 'desc' };

const collator = new Intl.Collator('pl', { sensitivity: 'base' });

/** Porównanie bez wielkości liter i polskich znaków: "zabka" znajdzie "Żabka". */
const normalize = (text: string) =>
  text
    .toLocaleLowerCase('pl')
    .replace(/ł/g, 'l')
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '');

export function filterLeads(leads: Lead[], filters: LeadFilters): Lead[] {
  const query = normalize(filters.query.trim());
  const today = todayIso();

  return leads.filter((lead) => {
    if (filters.cities.length > 0 && !filters.cities.includes(lead.city)) return false;
    if (filters.categoryIds.length > 0 && !filters.categoryIds.includes(lead.categoryId)) return false;
    if (filters.dueOnly && !isDue(lead, today)) return false;
    // Kafelek "Do zrobienia" pokazuje przypomnienia ze wszystkich zakładek.
    if (!filters.dueOnly && filters.view !== 'all' && viewOfStage(lead.stage) !== filters.view) return false;
    if (filters.statusGroup !== 'all' && statusGroupOf(lead.status) !== filters.statusGroup) return false;
    if (filters.scoreTier !== 'all' && lead.score.tier !== filters.scoreTier) return false;
    if (filters.searchRunId !== null && lead.firstSearchRunId !== filters.searchRunId) return false;

    if (query) {
      const haystack = normalize([lead.name, lead.address, lead.phone, lead.websiteUri, lead.notes].join(' '));
      if (!haystack.includes(query)) return false;
    }
    return true;
  });
}

export function sortLeads(leads: Lead[], sort: SortState): Lead[] {
  const factor = sort.direction === 'asc' ? 1 : -1;
  const byReviews = (a: Lead, b: Lead) => (b.userRatingCount ?? 0) - (a.userRatingCount ?? 0);

  const compare: Record<SortKey, (a: Lead, b: Lead) => number> = {
    score: (a, b) => factor * (a.score.value - b.score.value) || byReviews(a, b),
    // Jak w CSV: gorące → WordPress → reszta, w grupie więcej opinii wyżej.
    priority: (a, b) => factor * (a.priority - b.priority) || byReviews(a, b),
    name: (a, b) => factor * collator.compare(a.name, b.name),
    rating: (a, b) => factor * ((a.rating ?? 0) - (b.rating ?? 0)) || byReviews(a, b),
    reviews: (a, b) => factor * ((a.userRatingCount ?? 0) - (b.userRatingCount ?? 0)),
    firstSeen: (a, b) => factor * a.firstSeenAt.localeCompare(b.firstSeenAt),
  };

  return [...leads].sort(compare[sort.key]);
}
