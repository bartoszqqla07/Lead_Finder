import { viewOfStage, type LeadView } from '../filters';
import type { Lead } from '../types';

interface Props {
  leads: Lead[];
  view: LeadView;
  onChange: (view: LeadView) => void;
}

export const viewLabel: Record<LeadView, string> = {
  new: 'Nowe',
  contact: 'W kontakcie',
  later: 'Na później',
  client: 'Klienci',
  rejected: 'Odrzucone',
  all: 'Wszystkie',
};

const TABS = Object.entries(viewLabel).map(([value, label]) => ({ value: value as LeadView, label }));

/**
 * Zakładki nad listą według etapu kontaktu. "Nowe" to lista robocza: po zmianie etapu salon
 * przechodzi do innej zakładki, więc ta lista maleje w miarę pracy.
 */
export function ViewTabs({ leads, view, onChange }: Props) {
  const counts = new Map<LeadView, number>([['all', leads.length]]);
  for (const lead of leads) {
    const leadView = viewOfStage(lead.stage);
    counts.set(leadView, (counts.get(leadView) ?? 0) + 1);
  }

  return (
    <div className="view-tabs" role="tablist" aria-label="Etap kontaktu">
      {TABS.map((tab) => (
        <button
          key={tab.value}
          role="tab"
          aria-selected={view === tab.value}
          className={`view-tab ${view === tab.value ? 'active' : ''}`}
          onClick={() => onChange(tab.value)}
        >
          {tab.label}
          <span className="view-tab-count">{counts.get(tab.value) ?? 0}</span>
        </button>
      ))}
    </div>
  );
}
