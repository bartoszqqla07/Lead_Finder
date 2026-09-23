import { useState, type FormEvent } from 'react';
import type { Category, StartSearch } from '../types';

interface Props {
  categories: Category[];
  knownCities: string[];
  isRunning: boolean;
  hasApiKey: boolean;
  onStart: (search: StartSearch) => Promise<void>;
}

const PAGE_OPTIONS = [1, 2, 3] as const;

export function SearchPanel({ categories, knownCities, isRunning, hasApiKey, onStart }: Props) {
  const [city, setCity] = useState('');
  // null = wszystkie kategorie (także te dodane później do categories.json)
  const [selected, setSelected] = useState<Set<string> | null>(null);
  const [pages, setPages] = useState<number>(3);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isSelected = (id: string) => selected === null || selected.has(id);
  const selectedCount = selected === null ? categories.length : selected.size;
  const maxRequests = selectedCount * pages;

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
      await onStart({ city: city.trim(), categoryIds: selected ? [...selected] : [], pages });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const canSubmit = hasApiKey && !isRunning && !isSubmitting && city.trim().length > 0 && selectedCount > 0;

  return (
    <form className="card" onSubmit={submit}>
      <h2 className="card-title">Nowe wyszukiwanie</h2>

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

      <p className="hint">
        Maks. <strong>{maxRequests}</strong> zapytań do Google. Sprawdzenie stron trwa zwykle 2–4 min.
      </p>

      {error && <p className="form-error">{error}</p>}

      <button className="button button-primary button-block" type="submit" disabled={!canSubmit}>
        {isRunning ? 'Wyszukiwanie w toku…' : isSubmitting ? 'Uruchamiam…' : 'Szukaj leadów'}
      </button>
    </form>
  );
}
