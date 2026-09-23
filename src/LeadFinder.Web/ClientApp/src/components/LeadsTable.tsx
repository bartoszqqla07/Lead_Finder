import type { SortKey, SortState } from '../filters';
import { formatDate, formatRating, websiteHost } from '../labels';
import type { Lead } from '../types';
import { StageBadge, StatusBadge } from './Badges';

interface Props {
  leads: Lead[];
  isLoading: boolean;
  hasAnyLeads: boolean;
  sort: SortState;
  onSort: (sort: SortState) => void;
  selectedId: number | null;
  onSelect: (id: number) => void;
}

export function LeadsTable({ leads, isLoading, hasAnyLeads, sort, onSort, selectedId, onSelect }: Props) {
  if (isLoading) return <div className="empty">Wczytuję leady…</div>;

  if (!hasAnyLeads) {
    return (
      <div className="empty">
        <p className="empty-title">Jeszcze nie ma leadów</p>
        <p className="muted">Wpisz miasto po lewej i kliknij „Szukaj leadów”.</p>
      </div>
    );
  }

  if (leads.length === 0) {
    return (
      <div className="empty">
        <p className="empty-title">Nic nie pasuje do filtrów</p>
      </div>
    );
  }

  const header = (key: SortKey, label: string, className = '') => {
    const isActive = sort.key === key;
    const nextDirection = isActive && sort.direction === 'asc' ? 'desc' : 'asc';
    return (
      <th className={className} aria-sort={isActive ? (sort.direction === 'asc' ? 'ascending' : 'descending') : 'none'}>
        <button className="sort-button" onClick={() => onSort({ key, direction: nextDirection })}>
          {label}
          <span className="sort-indicator" aria-hidden="true">
            {isActive ? (sort.direction === 'asc' ? '▲' : '▼') : ''}
          </span>
        </button>
      </th>
    );
  };

  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr>
            {header('name', 'Firma')}
            {header('priority', 'Status')}
            <th>Strona</th>
            {header('rating', 'Ocena', 'num')}
            <th>Etap</th>
            {header('firstSeen', 'Dodano', 'num')}
          </tr>
        </thead>
        <tbody>
          {leads.map((lead) => {
            const host = websiteHost(lead);
            return (
              <tr
                key={lead.id}
                className={lead.id === selectedId ? 'selected' : ''}
                onClick={() => onSelect(lead.id)}
                onKeyDown={(e) => e.key === 'Enter' && onSelect(lead.id)}
                tabIndex={0}
              >
                <td>
                  <div className="cell-title">{lead.name}</div>
                  <div className="cell-sub">
                    {lead.categoryName} · {lead.address ?? lead.city}
                  </div>
                </td>
                <td>
                  <StatusBadge status={lead.status} />
                  {lead.checkNote && lead.status !== 'NoWebsite' && (
                    <div className="cell-sub truncate" title={lead.checkNote}>
                      {lead.checkNote}
                    </div>
                  )}
                  {lead.profilePlatform && <div className="cell-sub">tylko {lead.profilePlatform}</div>}
                </td>
                <td className="truncate">
                  {host ? (
                    <a href={lead.websiteUri!} target="_blank" rel="noreferrer" onClick={(e) => e.stopPropagation()}>
                      {host}
                    </a>
                  ) : (
                    <span className="muted">—</span>
                  )}
                </td>
                <td className="num">
                  {lead.rating !== null ? (
                    <>
                      <span className="star">★</span> {formatRating(lead.rating)}
                      <div className="cell-sub">{lead.userRatingCount ?? 0} opinii</div>
                    </>
                  ) : (
                    <span className="muted">—</span>
                  )}
                </td>
                <td>
                  <StageBadge stage={lead.stage} />
                </td>
                <td className="num muted">{formatDate(lead.firstSeenAt)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
