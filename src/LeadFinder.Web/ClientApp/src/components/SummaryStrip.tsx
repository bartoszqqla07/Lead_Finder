import type { LeadFilters } from '../filters';
import { statusGroupOf } from '../labels';
import type { Lead } from '../types';

interface Props {
  leads: Lead[];
  filters: LeadFilters;
  onFilter: (changes: Partial<LeadFilters>) => void;
}

/** Kafelki z liczbami, które działają też jako szybkie filtry. */
export function SummaryStrip({ leads, filters, onFilter }: Props) {
  const open = (l: Lead) => l.stage !== 'Client' && l.stage !== 'Rejected';

  const tiles = [
    {
      key: 'hot',
      label: 'Gorące leady',
      hint: 'brak strony / nie działa',
      count: leads.filter((l) => statusGroupOf(l.status) === 'hot' && open(l)).length,
      active: filters.statusGroup === 'hot' && filters.stage === 'open',
      apply: { statusGroup: 'hot', stage: 'open' },
    },
    {
      key: 'wordpress',
      label: 'WordPress',
      hint: 'do odświeżenia',
      count: leads.filter((l) => l.status === 'WordPress' && open(l)).length,
      active: filters.statusGroup === 'wordpress' && filters.stage === 'open',
      apply: { statusGroup: 'wordpress', stage: 'open' },
    },
    {
      key: 'contact',
      label: 'W kontakcie',
      hint: 'skontaktowani + odpowiedzi',
      count: leads.filter((l) => l.stage === 'Contacted' || l.stage === 'Replied').length,
      active: filters.stage === 'inContact',
      apply: { stage: 'inContact' },
    },
    {
      key: 'client',
      label: 'Klienci',
      hint: 'wygrane',
      count: leads.filter((l) => l.stage === 'Client').length,
      active: filters.stage === 'Client',
      apply: { stage: 'Client' },
    },
  ] as const;

  return (
    <div className="summary">
      {tiles.map((tile) => (
        <button
          key={tile.key}
          className={`tile tile-${tile.key} ${tile.active ? 'active' : ''}`}
          onClick={() => onFilter(tile.active ? {} : tile.apply)}
          aria-pressed={tile.active}
        >
          <span className="tile-count">{tile.count}</span>
          <span className="tile-label">{tile.label}</span>
          <span className="tile-hint">{tile.hint}</span>
        </button>
      ))}
    </div>
  );
}
