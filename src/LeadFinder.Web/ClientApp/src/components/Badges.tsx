import { scoreTierLabel, stageLabel, statusBadge } from '../labels';
import type { LeadScore, LeadStatus, OutreachStage } from '../types';

export function StatusBadge({ status }: { status: LeadStatus }) {
  const { label, tone } = statusBadge[status];
  return <span className={`badge badge-${tone}`}>{label}</span>;
}

export function StageBadge({ stage }: { stage: OutreachStage }) {
  return <span className={`stage stage-${stage.toLowerCase()}`}>{stageLabel(stage)}</span>;
}

/** Wynik 0–100 w kolorze poziomu szansy (zielony – wysoka, żółty – średnia, szary – niska). */
export function ScoreBadge({ score }: { score: LeadScore }) {
  return (
    <span
      className={`score score-${score.tier.toLowerCase()}`}
      title={`${scoreTierLabel[score.tier]}: ${score.value}/100`}
    >
      {score.value}
    </span>
  );
}
