import { useState } from 'react';
import { defaultFilters, type LeadFilters } from '../filters';
import { useIsMobile } from '../hooks/useMediaQuery';
import { MultiSelect } from './MultiSelect';
import { formatDateTime, plural } from '../labels';
import type { Category, SearchRun } from '../types';

interface Props {
  filters: LeadFilters;
  onChange: (filters: LeadFilters) => void;
  cities: string[];
  categories: Category[];
  activeRun: SearchRun | null;
  visibleCount: number;
  totalCount: number;
  onExport: () => void;
}

export function FilterBar({
  filters,
  onChange,
  cities,
  categories,
  activeRun,
  visibleCount,
  totalCount,
  onExport,
}: Props) {
  const isMobile = useIsMobile();
  const [showFilters, setShowFilters] = useState(false);
  const set = <K extends keyof LeadFilters>(key: K, value: LeadFilters[K]) => onChange({ ...filters, [key]: value });
  // Zakładka (etap) nie jest filtrem do czyszczenia – zostaje po "Wyczyść filtry".
  const isFiltered = JSON.stringify({ ...filters, view: defaultFilters.view }) !== JSON.stringify(defaultFilters);
  const activeSelects = [
    filters.cities.length > 0,
    filters.categoryIds.length > 0,
    filters.statusGroup !== defaultFilters.statusGroup,
    filters.scoreTier !== defaultFilters.scoreTier,
  ].filter(Boolean).length;

  return (
    <div className="filterbar">
      <div className="filter-row">
        <input
          className="input search-input"
          type="search"
          placeholder={isMobile ? 'Szukaj…' : 'Szukaj po nazwie, adresie, telefonie, notatkach…'}
          value={filters.query}
          onChange={(e) => set('query', e.target.value)}
        />

        {isMobile && (
          <button
            className={`button filter-toggle ${activeSelects > 0 ? 'has-active' : ''}`}
            onClick={() => setShowFilters(!showFilters)}
            aria-expanded={showFilters}
          >
            Filtry{activeSelects > 0 ? ` (${activeSelects})` : ''}
          </button>
        )}

        {(!isMobile || showFilters) && (
          <>
            <MultiSelect
              allLabel="Wszystkie miasta"
              pluralLabel="miasta"
              options={cities.map((city) => ({ value: city, label: city }))}
              selected={filters.cities}
              onChange={(selected) => set('cities', selected)}
            />

            <MultiSelect
              allLabel="Wszystkie kategorie"
              pluralLabel={plural(filters.categoryIds.length, 'kategoria', 'kategorie', 'kategorii')}
              options={categories.map((category) => ({ value: category.id, label: category.query }))}
              selected={filters.categoryIds}
              onChange={(selected) => set('categoryIds', selected)}
            />

            <select
              className="select"
              value={filters.statusGroup}
              onChange={(e) => set('statusGroup', e.target.value as LeadFilters['statusGroup'])}
              aria-label="Status strony"
            >
              <option value="all">Każdy status strony</option>
              <option value="hot">Gorące (brak / nie działa)</option>
              <option value="wordpress">WordPress</option>
              <option value="other">Ma stronę</option>
            </select>

            <select
              className="select"
              value={filters.scoreTier}
              onChange={(e) => set('scoreTier', e.target.value as LeadFilters['scoreTier'])}
              aria-label="Szansa na zlecenie"
            >
              <option value="all">Każda szansa</option>
              <option value="High">Wysoka szansa (65+)</option>
              <option value="Medium">Średnia szansa (40–64)</option>
              <option value="Low">Niska szansa (poniżej 40)</option>
            </select>
          </>
        )}
      </div>

      <div className="filter-row filter-row-meta">
        <span className="muted">
          Pokazano <strong>{visibleCount}</strong> z {totalCount} {plural(totalCount, 'leada', 'leadów', 'leadów')}
        </span>

        {activeRun && (
          <span className="pill">
            Nowe z wyszukiwania: {activeRun.city}, {formatDateTime(activeRun.startedAt)}
            <button onClick={() => set('searchRunId', null)} aria-label="Usuń filtr wyszukiwania">
              ×
            </button>
          </span>
        )}

        {filters.dueOnly && (
          <span className="pill">
            Do zrobienia: dziś i zaległe
            <button onClick={() => set('dueOnly', false)} aria-label="Usuń filtr przypomnień">
              ×
            </button>
          </span>
        )}

        {isFiltered && (
          <button className="link-button" onClick={() => onChange({ ...defaultFilters, view: filters.view })}>
            Wyczyść filtry
          </button>
        )}

        <button className="button button-small push-right" onClick={onExport} disabled={visibleCount === 0}>
          Eksport CSV ({visibleCount})
        </button>
      </div>
    </div>
  );
}
