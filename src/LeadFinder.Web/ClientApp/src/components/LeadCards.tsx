import type { SortState } from '../filters';
import { formatRating, websiteHost } from '../labels';
import type { Lead } from '../types';
import { ScoreBadge, StageBadge, StatusBadge } from './Badges';

interface Props {
  leads: Lead[];
  sort: SortState;
  onSort: (sort: SortState) => void;
  onSelect: (id: number) => void;
}

const SORT_OPTIONS: { value: string; label: string; sort: SortState }[] = [
  { value: 'score', label: 'Największa szansa', sort: { key: 'score', direction: 'desc' } },
  { value: 'priority', label: 'Najpierw gorące', sort: { key: 'priority', direction: 'asc' } },
  { value: 'newest', label: 'Najnowsze', sort: { key: 'firstSeen', direction: 'desc' } },
  { value: 'rating', label: 'Najlepiej oceniane', sort: { key: 'rating', direction: 'desc' } },
  { value: 'reviews', label: 'Najwięcej opinii', sort: { key: 'reviews', direction: 'desc' } },
  { value: 'name', label: 'Nazwa A–Z', sort: { key: 'name', direction: 'asc' } },
];

const sortValue = (sort: SortState) =>
  SORT_OPTIONS.find((o) => o.sort.key === sort.key && o.sort.direction === sort.direction)?.value ?? 'score';

/** Lista leadów na telefon: jedna karta na lead, sortowanie z listy rozwijanej zamiast nagłówków tabeli. */
export function LeadCards({ leads, sort, onSort, onSelect }: Props) {
  return (
    <div className="lead-cards-wrap">
      <label className="mobile-sort">
        <span className="muted">Sortuj:</span>
        <select
          className="select"
          value={sortValue(sort)}
          onChange={(e) => {
            const option = SORT_OPTIONS.find((o) => o.value === e.target.value);
            if (option) onSort(option.sort);
          }}
        >
          {SORT_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </label>

      <ul className="lead-cards">
        {leads.map((lead) => {
          const host = websiteHost(lead);
          return (
            <li key={lead.id}>
              <button className="lead-card" onClick={() => onSelect(lead.id)}>
                <span className="lead-card-top">
                  <span className="cell-title">{lead.name}</span>
                  <StageBadge stage={lead.stage} />
                </span>
                <span className="cell-sub">
                  {lead.categoryName} · {lead.address ?? lead.city}
                </span>
                <span className="lead-card-meta">
                  <ScoreBadge score={lead.score} />
                  <StatusBadge status={lead.status} />
                  {lead.rating !== null && (
                    <span className="muted">
                      <span className="star">★</span> {formatRating(lead.rating)} ({lead.userRatingCount ?? 0})
                    </span>
                  )}
                  {host && <span className="muted truncate">{lead.profilePlatform ?? host}</span>}
                </span>
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
