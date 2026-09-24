import { isDue } from '../filters';
import { formatDate } from '../labels';
import type { Lead } from '../types';

/** Termin następnego kroku pod etapem; na czerwono, gdy przypada dziś albo jest zaległy. */
export function NextActionDate({ lead }: { lead: Lead }) {
  if (!lead.nextActionDate) return null;
  return (
    <span className={`next-action-badge ${isDue(lead) ? 'due-now' : ''}`} title="Następny krok">
      ⏰ {formatDate(lead.nextActionDate)}
    </span>
  );
}
