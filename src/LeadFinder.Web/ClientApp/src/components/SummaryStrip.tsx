import { isDue, todayIso, viewOfStage, type LeadFilters } from '../filters';
import { statusGroupOf } from '../labels';
import type { Lead } from '../types';

interface Props {
  leads: Lead[];
  filters: LeadFilters;
  onFilter: (changes: Partial<LeadFilters>) => void;
}

/**
 * Kafelki z liczbami, które działają też jako szybkie filtry. "Gorące" i "WordPress" liczą się
 * w bieżącej zakładce; "Do zrobienia" – ze wszystkich zakładek (przypomnienie nie może zginąć).
 */
export function SummaryStrip({ leads, filters, onFilter }: Props) {
  const today = todayIso();
  const inView = leads.filter((l) => filters.view === 'all' || viewOfStage(l.stage) === filters.view);
  const dueCount = leads.filter((l) => isDue(l, today)).length;
  const overdueCount = leads.filter((l) => isDue(l, today) && l.nextActionDate! < today).length;

  const tiles = [
    {
      key: 'due',
      label: 'Do zrobienia',
      hint: overdueCount > 0 ? `w tym ${overdueCount} zaległe` : 'przypomnienia na dziś',
      count: dueCount,
      active: filters.dueOnly,
      apply: { dueOnly: true },
    },
    {
      key: 'hot',
      label: 'Gorące leady',
      hint: 'brak strony / nie działa',
      count: inView.filter((l) => statusGroupOf(l.status) === 'hot').length,
      active: !filters.dueOnly && filters.statusGroup === 'hot',
      apply: { statusGroup: 'hot' },
    },
    {
      key: 'wordpress',
      label: 'WordPress',
      hint: 'do odświeżenia',
      count: inView.filter((l) => l.status === 'WordPress').length,
      active: !filters.dueOnly && filters.statusGroup === 'wordpress',
      apply: { statusGroup: 'wordpress' },
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
