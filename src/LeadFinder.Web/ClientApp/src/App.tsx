import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from './api';
import { FilterBar } from './components/FilterBar';
import { LeadDrawer } from './components/LeadDrawer';
import { LeadsTable } from './components/LeadsTable';
import { SearchHistory } from './components/SearchHistory';
import { SearchPanel } from './components/SearchPanel';
import { SearchProgressCard } from './components/SearchProgressCard';
import { SettingsDialog } from './components/SettingsDialog';
import { SummaryStrip } from './components/SummaryStrip';
import { Toast, useToast } from './components/Toast';
import { ViewTabs, viewLabel } from './components/ViewTabs';
import {
  defaultFilters,
  defaultSort,
  filterLeads,
  sortLeads,
  viewOfStage,
  type LeadFilters,
  type SortState,
} from './filters';
import { useHashSelection } from './hooks/useHashSelection';
import { usePersistentState } from './hooks/usePersistentState';
import { useIsMobile } from './hooks/useMediaQuery';
import { useSearchJob } from './hooks/useSearchJob';
import { plural } from './labels';
import type { Category, Lead, LeadChanges, SearchRun, Settings } from './types';

/** W wąskim oknie zamienia panel w zwijaną sekcję, żeby lista leadów była od razu pod ręką. */
function MobileSection({
  enabled,
  title,
  defaultOpen,
  children,
}: {
  enabled: boolean;
  title: string;
  defaultOpen: boolean;
  children: ReactNode;
}) {
  if (!enabled) return <>{children}</>;
  return (
    <details className="card collapsible" open={defaultOpen}>
      <summary>{title}</summary>
      <div className="collapsible-body">{children}</div>
    </details>
  );
}

export function App() {
  const isMobile = useIsMobile();
  const [leads, setLeads] = useState<Lead[]>([]);
  const [runs, setRuns] = useState<SearchRun[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [settings, setSettings] = useState<Settings | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  // Filtry i sortowanie przetrwają zamknięcie aplikacji (localStorage).
  const [filters, setFilters] = usePersistentState<LeadFilters>('leadfinder.filters.v3', defaultFilters);
  const [sort, setSort] = usePersistentState<SortState>('leadfinder.sort.v2', defaultSort);
  const [selectedId, setSelectedId] = useHashSelection();
  const [settingsOpen, setSettingsOpen] = useState(false);
  const { toast, showToast } = useToast();

  const reload = useCallback(async () => {
    const [freshLeads, freshRuns] = await Promise.all([api.getLeads(), api.listSearches()]);
    setLeads(freshLeads);
    setRuns(freshRuns);
  }, []);

  useEffect(() => {
    Promise.all([reload(), api.getCategories().then(setCategories), api.getSettings().then(setSettings)])
      .catch((error: Error) => showToast(error.message, 'error'))
      .finally(() => setIsLoading(false));
  }, [reload, showToast]);

  const search = useSearchJob((run) => {
    reload().catch((error: Error) => showToast(error.message, 'error'));
    if (run.state === 'Completed') {
      showToast(
        `Gotowe: ${run.newCount} ${plural(run.newCount, 'nowy lead', 'nowe leady', 'nowych leadów')}.`,
        'success',
      );
    }
  });

  const cities = useMemo(
    () => [...new Set(leads.map((l) => l.city))].sort((a, b) => a.localeCompare(b, 'pl')),
    [leads],
  );
  const visibleLeads = useMemo(() => sortLeads(filterLeads(leads, filters), sort), [leads, filters, sort]);
  const selectedLead = leads.find((l) => l.id === selectedId) ?? null;

  const updateLead = async (id: number, changes: LeadChanges) => {
    const previous = leads.find((l) => l.id === id);
    const updated = await api.updateLead(id, changes);
    setLeads((current) => current.map((l) => (l.id === id ? updated : l)));

    // Zmiana etapu przenosi lead do innej zakładki – mówimy dokąd, żeby nie "zniknął" bez słowa.
    if (previous && viewOfStage(previous.stage) !== viewOfStage(updated.stage)) {
      showToast(`Przeniesiono do zakładki „${viewLabel[viewOfStage(updated.stage)]}”.`, 'success');
    }
  };

  const deleteLead = async (id: number, block: boolean) => {
    try {
      await api.deleteLead(id, block);
      setLeads((current) => current.filter((l) => l.id !== id));
      setSelectedId(null);
      showToast(block ? 'Usunięto. Ta firma nie pojawi się w kolejnych wyszukiwaniach.' : 'Lead usunięty.', 'success');
    } catch (error) {
      showToast((error as Error).message, 'error');
    }
  };

  const exportVisible = async () => {
    try {
      const { blob, fileName } = await api.exportLeads(
        visibleLeads.map((l) => l.id),
        filters.cities.length === 1 ? filters.cities[0]! : 'wybrane',
      );
      const url = URL.createObjectURL(blob);
      const link = Object.assign(document.createElement('a'), { href: url, download: fileName });
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      showToast((error as Error).message, 'error');
    }
  };

  const showNewFromRun = (runId: number) => {
    // Wszystkie firmy z tego wyszukiwania – także te, z którymi już był kontakt.
    setFilters({ ...defaultFilters, view: 'all', searchRunId: runId });
    setSort(defaultSort);
  };

  const handleSettingsSaved = (saved: Settings) => {
    setSettings(saved);
    // Podpis mógł się zmienić – szkice wiadomości generuje serwer, więc pobieramy je ponownie.
    reload().catch((error: Error) => showToast(error.message, 'error'));
  };

  const runById = (id: number | null) => runs.find((r) => r.id === id) ?? null;

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            <svg viewBox="0 0 24 24" width="18" height="18">
              <circle cx="10.5" cy="10.5" r="6" fill="none" stroke="currentColor" strokeWidth="2.4" />
              <path d="M15 15l5 5" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
            </svg>
          </span>
          <span className="brand-name">LeadFinder</span>
          <span className="brand-tagline">lokalne biznesy bez dobrej strony</span>
        </div>
        <button className="button button-ghost" onClick={() => setSettingsOpen(true)} aria-label="Ustawienia">
          <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
            <path
              fill="currentColor"
              d="M19.4 13a7.6 7.6 0 0 0 0-2l2-1.6-2-3.4-2.4 1a7.4 7.4 0 0 0-1.7-1L15 3.5h-4l-.3 2.5a7.4 7.4 0 0 0-1.7 1l-2.4-1-2 3.4 2 1.6a7.6 7.6 0 0 0 0 2l-2 1.6 2 3.4 2.4-1a7.4 7.4 0 0 0 1.7 1l.3 2.5h4l.3-2.5a7.4 7.4 0 0 0 1.7-1l2.4 1 2-3.4-2-1.6ZM13 15.5a3.5 3.5 0 1 1 0-7 3.5 3.5 0 0 1 0 7Z"
              transform="translate(-1)"
            />
          </svg>
          <span className="hide-mobile">Ustawienia</span>
        </button>
      </header>

      {settings && !settings.hasApiKey && (
        <div className="banner">
          <strong>Brak klucza Google Places API.</strong> Bez niego wyszukiwanie nie ruszy.
          <button className="link-button" onClick={() => setSettingsOpen(true)}>
            Dodaj klucz w ustawieniach →
          </button>
        </div>
      )}

      <div className="layout">
        <aside className="sidebar">
          <MobileSection enabled={isMobile} title="Nowe wyszukiwanie" defaultOpen={!isLoading && leads.length === 0}>
            <SearchPanel
              categories={categories}
              knownCities={cities}
              isRunning={search.isRunning}
              hasApiKey={settings?.hasApiKey ?? false}
              onStart={search.start}
            />
          </MobileSection>
          {search.run && <SearchProgressCard job={search} onShowNew={showNewFromRun} />}
          {runs.length > 0 && (
            <MobileSection enabled={isMobile} title="Historia wyszukiwań" defaultOpen={false}>
              <SearchHistory runs={runs} activeRunId={filters.searchRunId} onSelect={showNewFromRun} />
            </MobileSection>
          )}
        </aside>

        <main className="main">
          <ViewTabs
            leads={leads}
            view={filters.view}
            onChange={(view) => setFilters({ ...filters, view, dueOnly: false })}
          />
          <SummaryStrip
            leads={filters.cities.length > 0 ? leads.filter((l) => filters.cities.includes(l.city)) : leads}
            filters={filters}
            onFilter={(changes) =>
              setFilters({ ...defaultFilters, cities: filters.cities, view: filters.view, ...changes })
            }
          />
          <FilterBar
            filters={filters}
            onChange={setFilters}
            cities={cities}
            categories={categories}
            activeRun={runById(filters.searchRunId)}
            visibleCount={visibleLeads.length}
            totalCount={leads.length}
            onExport={exportVisible}
          />
          <LeadsTable
            leads={visibleLeads}
            isLoading={isLoading}
            hasAnyLeads={leads.length > 0}
            sort={sort}
            onSort={setSort}
            selectedId={selectedId}
            onSelect={setSelectedId}
          />
        </main>
      </div>

      {selectedLead && (
        <LeadDrawer
          key={selectedLead.id}
          lead={selectedLead}
          onClose={() => setSelectedId(null)}
          onUpdate={updateLead}
          onDelete={deleteLead}
          onError={(message) => showToast(message, 'error')}
        />
      )}

      {settingsOpen && settings && (
        <SettingsDialog settings={settings} onClose={() => setSettingsOpen(false)} onSaved={handleSettingsSaved} />
      )}

      <Toast toast={toast} />
    </div>
  );
}
