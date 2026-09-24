import { formatDateTime, runStateLabel } from '../labels';
import type { SearchRun } from '../types';

interface Props {
  runs: SearchRun[];
  activeRunId: number | null;
  onSelect: (runId: number) => void;
}

export function SearchHistory({ runs, activeRunId, onSelect }: Props) {
  if (runs.length === 0) return null;

  return (
    <section className="card">
      <h2 className="card-title">Historia wyszukiwań</h2>
      <ul className="history">
        {runs.slice(0, 8).map((run) => (
          <li key={run.id}>
            <button
              className={`history-item ${run.id === activeRunId ? 'active' : ''}`}
              onClick={() => onSelect(run.id)}
              disabled={run.state !== 'Completed' || run.newCount === 0}
              title={[
                run.error,
                run.categories.join(', '),
                run.apiRequests !== null ? `${run.apiRequests} zapytań do Google` : null,
                run.minScore > 0 ? `tylko szansa ${run.minScore}+` : null,
              ]
                .filter(Boolean)
                .join(' · ')}
            >
              <span className="history-main">
                <strong>{run.city}</strong>
                <span className="muted"> · {formatDateTime(run.startedAt)}</span>
              </span>
              <span className={`history-meta state-${run.state.toLowerCase()}`}>
                {run.state === 'Completed' ? `+${run.newCount} nowych` : runStateLabel[run.state]}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
