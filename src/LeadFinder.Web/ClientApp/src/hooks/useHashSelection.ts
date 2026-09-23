import { useCallback, useEffect, useState } from 'react';

const PREFIX = '#lead/';

const readHash = (): number | null => {
  if (!window.location.hash.startsWith(PREFIX)) return null;
  const id = Number(window.location.hash.slice(PREFIX.length));
  return Number.isInteger(id) && id > 0 ? id : null;
};

/**
 * Otwarty lead zapisany w adresie (#lead/42): przycisk "wstecz" zamyka panel,
 * a link do konkretnego leada można zapisać w zakładkach lub notatkach.
 */
export function useHashSelection(): [number | null, (id: number | null) => void] {
  const [selectedId, setSelectedId] = useState<number | null>(readHash);

  useEffect(() => {
    const onHashChange = () => setSelectedId(readHash());
    window.addEventListener('hashchange', onHashChange);
    return () => window.removeEventListener('hashchange', onHashChange);
  }, []);

  const select = useCallback((id: number | null) => {
    if (id === null) {
      // Wróć w historii, jeśli panel otwarto kliknięciem – inaczej po prostu wyczyść adres.
      if (window.history.state?.leadPanel) window.history.back();
      else window.history.replaceState(null, '', window.location.pathname + window.location.search);
      setSelectedId(null);
      return;
    }
    const hash = `${PREFIX}${id}`;
    if (window.location.hash === hash) return;
    const hadPanel = readHash() !== null;
    // Przełączanie między leadami nie zaśmieca historii – "wstecz" zawsze zamyka panel.
    if (hadPanel) window.history.replaceState({ leadPanel: true }, '', hash);
    else window.history.pushState({ leadPanel: true }, '', hash);
    setSelectedId(id);
  }, []);

  return [selectedId, select];
}
