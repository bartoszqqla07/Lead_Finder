import { useState, type FormEvent } from 'react';
import { plural } from '../labels';
import type { Category, Region, StartSearch, Usage } from '../types';
import { MultiSelect } from './MultiSelect';

interface Props {
  categories: Category[];
  regions: Region[];
  knownCities: string[];
  usage: Usage | null;
  isRunning: boolean;
  hasApiKey: boolean;
  onStart: (search: StartSearch) => Promise<void>;
}

type Scope = 'city' | 'region' | 'poland';

const PAGE_OPTIONS = [1, 2, 3] as const;

/** Orientacyjna cena Text Search Enterprise ponad darmowy limit (cennik Google z marca 2025). */
const USD_PER_1000_REQUESTS = 35;

const MIN_SCORE_OPTIONS = [
  { value: 0, label: 'Wszystkie leady' },
  { value: 40, label: 'Szansa 40+ (średnia i wysoka)' },
  { value: 65, label: 'Szansa 65+ (wysoka)' },
  { value: 75, label: 'Szansa 75+' },
  { value: 85, label: 'Szansa 85+' },
] as const;

export function SearchPanel({ categories, regions, knownCities, usage, isRunning, hasApiKey, onStart }: Props) {
  const [scope, setScope] = useState<Scope>('city');
  const [city, setCity] = useState('');
  const [regionId, setRegionId] = useState('');
  // Miasta województwa: puste = wszystkie z listy.
  const [regionCities, setRegionCities] = useState<string[]>([]);
  const [polandCapitalsOnly, setPolandCapitalsOnly] = useState(true);
  // null = wszystkie kategorie (także te dodane później do categories.json)
  const [selected, setSelected] = useState<Set<string> | null>(null);
  const [pages, setPages] = useState<number>(3);
  const [minScore, setMinScore] = useState<number>(0);
  const [acceptOverLimit, setAcceptOverLimit] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const region = regions.find((r) => r.id === regionId) ?? null;

  const { cities, label } = ((): { cities: string[]; label: string } => {
    switch (scope) {
      case 'city':
        return { cities: city.trim() ? [city.trim()] : [], label: city.trim() };
      case 'region':
        if (!region) return { cities: [], label: '' };
        return {
          cities: regionCities.length > 0 ? region.cities.filter((c) => regionCities.includes(c)) : region.cities,
          label: `województwo ${region.name}`,
        };
      case 'poland': {
        const all = polandCapitalsOnly ? regions.map((r) => r.cities[0]!) : regions.flatMap((r) => r.cities);
        return { cities: all, label: polandCapitalsOnly ? 'Polska – miasta wojewódzkie' : 'Polska – wszystkie miasta' };
      }
    }
  })();

  const isSelected = (id: string) => selected === null || selected.has(id);
  const selectedCount = selected === null ? categories.length : selected.size;
  const maxRequests = cities.length * selectedCount * pages;
  const overLimitBy = usage ? Math.max(0, maxRequests - usage.remaining) : 0;
  const overLimitCost = (overLimitBy / 1000) * USD_PER_1000_REQUESTS;

  const toggle = (id: string) => {
    const next = new Set(selected ?? categories.map((c) => c.id));
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setSelected(next.size === categories.length ? null : next);
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await onStart({
        label,
        cities,
        categoryIds: selected ? [...selected] : [],
        pages,
        minScore,
        acceptOverLimit,
      });
      setAcceptOverLimit(false);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const canSubmit =
    hasApiKey &&
    !isRunning &&
    !isSubmitting &&
    cities.length > 0 &&
    selectedCount > 0 &&
    (overLimitBy === 0 || acceptOverLimit);

  return (
    <form className="card" onSubmit={submit}>
      <h2 className="card-title">Nowe wyszukiwanie</h2>

      {usage && <UsageMeter usage={usage} />}

      <div className="field">
        <span className="field-label">Zakres</span>
        <div className="segmented" role="radiogroup" aria-label="Zakres wyszukiwania">
          {(
            [
              ['city', 'Miasto'],
              ['region', 'Województwo'],
              ['poland', 'Cała Polska'],
            ] as const
          ).map(([value, text]) => (
            <button
              key={value}
              type="button"
              role="radio"
              aria-checked={scope === value}
              className={scope === value ? 'active' : ''}
              onClick={() => setScope(value)}
            >
              {text}
            </button>
          ))}
        </div>
      </div>

      {scope === 'city' && (
        <label className="field">
          <span className="field-label">Miasto</span>
          <input
            className="input"
            value={city}
            onChange={(e) => setCity(e.target.value)}
            placeholder="np. Katowice"
            list="known-cities"
            autoComplete="off"
          />
          <datalist id="known-cities">
            {knownCities.map((c) => (
              <option key={c} value={c} />
            ))}
          </datalist>
        </label>
      )}

      {scope === 'region' && (
        <div className="field">
          <span className="field-label">Województwo</span>
          <select
            className="select select-block"
            value={regionId}
            onChange={(e) => {
              setRegionId(e.target.value);
              setRegionCities([]);
            }}
          >
            <option value="">Wybierz…</option>
            {regions.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name} ({r.cities.length} {plural(r.cities.length, 'miasto', 'miasta', 'miast')})
              </option>
            ))}
          </select>
          {region && (
            <MultiSelect
              allLabel={`Wszystkie miasta (${region.cities.length})`}
              pluralLabel="miast"
              options={region.cities.map((c) => ({ value: c, label: c }))}
              selected={regionCities}
              onChange={setRegionCities}
            />
          )}
        </div>
      )}

      {scope === 'poland' && (
        <div className="field">
          <span className="field-label">Miasta</span>
          <div className="segmented segmented-2" role="radiogroup" aria-label="Miasta w Polsce">
            <button
              type="button"
              role="radio"
              aria-checked={polandCapitalsOnly}
              className={polandCapitalsOnly ? 'active' : ''}
              onClick={() => setPolandCapitalsOnly(true)}
            >
              Wojewódzkie ({regions.length})
            </button>
            <button
              type="button"
              role="radio"
              aria-checked={!polandCapitalsOnly}
              className={!polandCapitalsOnly ? 'active' : ''}
              onClick={() => setPolandCapitalsOnly(false)}
            >
              Wszystkie ({regions.reduce((sum, r) => sum + r.cities.length, 0)})
            </button>
          </div>
        </div>
      )}

      <div className="field">
        <div className="field-label-row">
          <span className="field-label">Kategorie</span>
          <button
            type="button"
            className="link-button small"
            onClick={() => setSelected(selected === null ? new Set() : null)}
          >
            {selected === null ? 'odznacz wszystkie' : 'zaznacz wszystkie'}
          </button>
        </div>
        <div className="chips">
          {categories.map((category) => (
            <button
              key={category.id}
              type="button"
              className={`chip ${isSelected(category.id) ? 'chip-on' : ''}`}
              aria-pressed={isSelected(category.id)}
              onClick={() => toggle(category.id)}
            >
              {category.query}
            </button>
          ))}
        </div>
      </div>

      <div className="field">
        <span className="field-label">Stron wyników na kategorię</span>
        <div className="segmented" role="radiogroup" aria-label="Stron wyników na kategorię">
          {PAGE_OPTIONS.map((option) => (
            <button
              key={option}
              type="button"
              role="radio"
              aria-checked={pages === option}
              className={pages === option ? 'active' : ''}
              onClick={() => setPages(option)}
            >
              {option} <span className="muted">· do {option * 20}</span>
            </button>
          ))}
        </div>
      </div>

      <label className="field">
        <span className="field-label">Zapisuj leady</span>
        <select className="select select-block" value={minScore} onChange={(e) => setMinScore(Number(e.target.value))}>
          {MIN_SCORE_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        {minScore >= 75 && (
          <span className="hint">
            Salony bez strony mają maks. ok. 70–80 pkt. Przy progu {minScore}+ zostaną głównie przestarzałe strony
            (stary WordPress, brak wersji na telefon).
          </span>
        )}
      </label>

      <div className={`estimate ${overLimitBy > 0 ? 'estimate-over' : ''}`}>
        <p>
          Maks. <strong>{maxRequests}</strong> {plural(maxRequests, 'zapytanie', 'zapytania', 'zapytań')} do Google
          {cities.length > 1 && (
            <>
              {' '}
              ({cities.length} {plural(cities.length, 'miasto', 'miasta', 'miast')} × {selectedCount} × {pages})
            </>
          )}
          .
        </p>
        {overLimitBy > 0 && (
          <>
            <p>
              Przekroczysz darmowy limit o ok. <strong>{overLimitBy}</strong> zapytań – to ok.{' '}
              <strong>{overLimitCost.toFixed(2).replace('.', ',')} USD</strong> według cennika Google.
            </p>
            <label className="consent-inline">
              <input type="checkbox" checked={acceptOverLimit} onChange={(e) => setAcceptOverLimit(e.target.checked)} />
              Rozumiem, że część zapytań będzie płatna
            </label>
          </>
        )}
        {cities.length > 1 && (
          <p className="hint">
            Skan wielu miast może potrwać nawet kilka godzin (sprawdzanie stron). Wyniki zapisują się po każdym mieście,
            więc można go przerwać bez utraty danych.
          </p>
        )}
      </div>

      {error && <p className="form-error">{error}</p>}

      <button className="button button-primary button-block" type="submit" disabled={!canSubmit}>
        {isRunning ? 'Wyszukiwanie w toku…' : isSubmitting ? 'Uruchamiam…' : 'Szukaj leadów'}
      </button>
    </form>
  );
}

/** Pasek zużycia darmowego limitu Google w bieżącym miesiącu. */
function UsageMeter({ usage }: { usage: Usage }) {
  const percentUsed = usage.limit > 0 ? Math.min(100, Math.round((usage.used / usage.limit) * 100)) : 100;
  const tone = percentUsed >= 90 ? 'danger' : percentUsed >= 70 ? 'warn' : 'ok';
  const monthName = new Intl.DateTimeFormat('pl-PL', { month: 'long' }).format(new Date(`${usage.month}-01`));

  return (
    <div
      className="usage"
      title="Liczone przez LeadFindera – nie obejmuje zapytań z innych aplikacji używających tego klucza."
    >
      <div className="usage-label">
        <span>Darmowy limit Google ({monthName})</span>
        <span className={`usage-remaining usage-${tone}`}>
          zostało ~{usage.remaining} z {usage.limit}
        </span>
      </div>
      <div className="usage-track">
        <div className={`usage-fill usage-fill-${tone}`} style={{ width: `${percentUsed}%` }} />
      </div>
      <span className="hint">
        ≈ {Math.floor(usage.remaining / 24)} pełnych wyszukiwań miasta
        {usage.includesEstimates ? ' · starsze wyszukiwania oszacowane' : ''}
      </span>
    </div>
  );
}
