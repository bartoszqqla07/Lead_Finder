import type { LeadFilters } from '../filters';
import type { Lead } from '../types';

interface Props {
  leads: Lead[];
  view: LeadFilters['view'];
  onChange: (view: LeadFilters['view']) => void;
}

/** Zakładki nad listą: główna lista i salony odłożone "na później" (osobno, żeby nie zaśmiecały pracy). */
export function ViewTabs({ leads, view, onChange }: Props) {
  const laterCount = leads.filter((l) => l.stage === 'Later').length;
  const tabs = [
    { value: 'main', label: 'Leady', count: leads.length - laterCount },
    { value: 'later', label: 'Na później', count: laterCount },
  ] as const;

  return (
    <div className="view-tabs" role="tablist" aria-label="Widok listy">
      {tabs.map((tab) => (
        <button
          key={tab.value}
          role="tab"
          aria-selected={view === tab.value}
          className={`view-tab ${view === tab.value ? 'active' : ''}`}
          onClick={() => onChange(tab.value)}
        >
          {tab.label}
          <span className="view-tab-count">{tab.count}</span>
        </button>
      ))}
    </div>
  );
}
