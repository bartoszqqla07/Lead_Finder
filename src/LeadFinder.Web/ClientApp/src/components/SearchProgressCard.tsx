import { useEffect, useRef } from 'react';
import type { SearchJobState } from '../hooks/useSearchJob';
import { plural } from '../labels';

interface Props {
  job: SearchJobState;
  onShowNew: (runId: number) => void;
}

const stageTitle = {
  Searching: 'Etap 1/2 · wyszukiwanie w Google',
  CheckingWebsites: 'Etap 2/2 · sprawdzanie stron WWW',
} as const;

export function SearchProgressCard({ job, onShowNew }: Props) {
  const logRef = useRef<HTMLOListElement>(null);
  const { run, events, isRunning, progress } = job;
  const finalEvent = events.find((e) => e.type !== 'progress');

  // Log przewija się do najnowszego wpisu.
  useEffect(() => {
    logRef.current?.scrollTo({ top: logRef.current.scrollHeight });
  }, [events.length]);

  if (!run) return null;

  const percent = progress && progress.total > 0 ? Math.round((progress.current / progress.total) * 100) : 0;
  const finishedRun = finalEvent?.run ?? null;

  return (
    <section className="card progress-card" aria-live="polite">
      <div className="card-title-row">
        <h2 className="card-title">Wyszukiwanie: {run.city}</h2>
        {!isRunning && (
          <button className="icon-button" onClick={job.dismiss} aria-label="Zamknij">
            ×
          </button>
        )}
      </div>

      {isRunning && (
        <>
          <div className="progress-label">
            <span>{progress?.stage ? stageTitle[progress.stage] : 'Uruchamianie…'}</span>
            {progress && (
              <span className="muted">
                {progress.current}/{progress.total}
              </span>
            )}
          </div>
          <div className="progress-track">
            <div className={`progress-fill ${progress ? '' : 'indeterminate'}`} style={{ width: `${percent}%` }} />
          </div>
        </>
      )}

      {finalEvent && (
        <div className={`result result-${finalEvent.type}`}>
          <p>{finalEvent.message}</p>
          {finishedRun && finalEvent.type === 'completed' && finishedRun.newCount > 0 && (
            <button className="button button-small" onClick={() => onShowNew(finishedRun.id)}>
              Pokaż {finishedRun.newCount}{' '}
              {plural(finishedRun.newCount, 'nowy lead', 'nowe leady', 'nowych leadów')}
            </button>
          )}
        </div>
      )}

      <ol className="log" ref={logRef}>
        {events
          .filter((e) => e.type === 'progress')
          .map((e, index) => (
            <li key={index}>{e.message}</li>
          ))}
      </ol>

      {isRunning && (
        <button className="button button-ghost button-block" onClick={() => void job.cancel()}>
          Przerwij
        </button>
      )}
    </section>
  );
}
