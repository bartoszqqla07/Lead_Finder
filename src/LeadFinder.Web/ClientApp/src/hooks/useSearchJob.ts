import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from '../api';
import type { SearchEvent, SearchRun, StartSearch } from '../types';
import { useLatest } from './useLatest';

export interface SearchJobState {
  run: SearchRun | null;
  events: SearchEvent[];
  isRunning: boolean;
  /** Ostatnie zdarzenie postępu – do paska postępu. */
  progress: SearchEvent | null;
  /** Przy skanie wielu miast: bieżące miasto ("Miasto 3/30: Gliwice"). */
  regionProgress: SearchEvent | null;
  start: (search: StartSearch) => Promise<void>;
  cancel: () => Promise<void>;
  dismiss: () => void;
}

/**
 * Stan wyszukiwania w toku. Zdarzenia przychodzą przez Server-Sent Events; po odświeżeniu strony
 * hook podłącza się ponownie do trwającego wyszukiwania, a serwer odtwarza całą historię zdarzeń.
 */
export function useSearchJob(onFinished: (run: SearchRun) => void): SearchJobState {
  const [run, setRun] = useState<SearchRun | null>(null);
  const [events, setEvents] = useState<SearchEvent[]>([]);
  const [isRunning, setIsRunning] = useState(false);
  const sourceRef = useRef<EventSource | null>(null);

  // Ref, żeby zmiana callbacku w rodzicu nie zrywała połączenia SSE.
  const onFinishedRef = useLatest(onFinished);

  const connect = useCallback((runId: number) => {
    sourceRef.current?.close();
    setEvents([]); // serwer i tak przyśle całą historię od początku
    setIsRunning(true);

    const source = new EventSource(api.searchEventsUrl(runId));
    sourceRef.current = source;

    source.onmessage = (message: MessageEvent<string>) => {
      const event = JSON.parse(message.data) as SearchEvent;
      setEvents((previous) => [...previous, event]);

      if (event.type !== 'progress') {
        // Zamykamy sami – inaczej EventSource po końcu strumienia łączyłby się ponownie.
        source.close();
        setIsRunning(false);
        if (event.run) {
          setRun(event.run);
          onFinishedRef.current(event.run);
        }
      }
    };

    source.onerror = () => {
      // Np. restart serwera. Zamiast pętli ponownych połączeń: sprawdź stan i zdecyduj.
      source.close();
      api
        .getCurrentSearch()
        .then((current) => {
          if (current?.isRunning && current.run.id === runId) connect(runId);
          else {
            setIsRunning(false);
            if (current?.run.id === runId) setRun(current.run);
          }
        })
        .catch(() => setIsRunning(false));
    };
  }, []);

  // Po załadowaniu strony: podłącz się do wyszukiwania, które mogło trwać w tle.
  useEffect(() => {
    api
      .getCurrentSearch()
      .then((current) => {
        if (current?.isRunning) {
          setRun(current.run);
          connect(current.run.id);
        }
      })
      .catch(() => {
        /* brak bieżącego wyszukiwania albo serwer jeszcze wstaje – nic do pokazania */
      });
    return () => sourceRef.current?.close();
  }, [connect]);

  const start = useCallback(
    async (search: StartSearch) => {
      const started = await api.startSearch(search);
      setRun(started);
      connect(started.id);
    },
    [connect],
  );

  const cancel = useCallback(async () => {
    if (run) await api.cancelSearch(run.id);
  }, [run]);

  const dismiss = useCallback(() => {
    if (isRunning) return;
    setRun(null);
    setEvents([]);
  }, [isRunning]);

  const latestFirst = [...events].reverse();
  const progress = latestFirst.find((e) => e.type === 'progress' && e.total > 0 && e.stage !== 'Region') ?? null;
  const regionProgress = latestFirst.find((e) => e.stage === 'Region') ?? null;

  return { run, events, isRunning, progress, regionProgress, start, cancel, dismiss };
}
