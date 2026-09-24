import { useEffect, useId, useRef, useState } from 'react';

interface Option {
  value: string;
  label: string;
}

interface Props {
  /** Etykieta, gdy nic nie jest zaznaczone, np. "Wszystkie kategorie". */
  allLabel: string;
  /** Rzeczownik do podsumowania, np. "kategorie" → "3 kategorie". */
  pluralLabel: string;
  options: Option[];
  selected: string[];
  onChange: (selected: string[]) => void;
}

/**
 * Lista rozwijana z polami wyboru. Nic nie zaznaczone = brak filtra ("wszystkie").
 * Zamyka się kliknięciem poza listą albo klawiszem Escape.
 */
export function MultiSelect({ allLabel, pluralLabel, options, selected, onChange }: Props) {
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const listId = useId();

  useEffect(() => {
    if (!isOpen) return;
    const onPointerDown = (e: PointerEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setIsOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setIsOpen(false);
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [isOpen]);

  const toggle = (value: string) =>
    onChange(selected.includes(value) ? selected.filter((v) => v !== value) : [...selected, value]);

  const summary =
    selected.length === 0
      ? allLabel
      : selected.length === 1
        ? (options.find((o) => o.value === selected[0])?.label ?? selected[0])
        : `${selected.length} ${pluralLabel}`;

  return (
    <div className="multiselect" ref={rootRef}>
      <button
        type="button"
        className={`select multiselect-button ${selected.length > 0 ? 'has-value' : ''}`}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-controls={listId}
        onClick={() => setIsOpen(!isOpen)}
      >
        <span className="multiselect-summary">{summary}</span>
      </button>

      {isOpen && (
        <div className="multiselect-menu" id={listId} role="listbox" aria-multiselectable="true">
          {options.map((option) => (
            <label key={option.value} className="multiselect-option">
              <input type="checkbox" checked={selected.includes(option.value)} onChange={() => toggle(option.value)} />
              {option.label}
            </label>
          ))}
          {options.length === 0 && <p className="hint">Brak opcji.</p>}
          {selected.length > 0 && (
            <button type="button" className="link-button small multiselect-clear" onClick={() => onChange([])}>
              Wyczyść wybór
            </button>
          )}
        </div>
      )}
    </div>
  );
}
