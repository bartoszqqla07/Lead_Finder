import { stageLabel, statusBadge } from '../labels';
import type { LeadStatus, OutreachStage } from '../types';

export function StatusBadge({ status }: { status: LeadStatus }) {
  const { label, tone } = statusBadge[status];
  return <span className={`badge badge-${tone}`}>{label}</span>;
}

export function StageBadge({ stage }: { stage: OutreachStage }) {
  return <span className={`stage stage-${stage.toLowerCase()}`}>{stageLabel(stage)}</span>;
}
