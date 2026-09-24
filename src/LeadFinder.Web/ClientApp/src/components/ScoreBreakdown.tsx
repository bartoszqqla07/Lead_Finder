import { scoreTierLabel } from '../labels';
import type { LeadScore } from '../types';

/** "Dlaczego taki wynik": pasek 0–100 i lista składników z punktami. */
export function ScoreBreakdown({ score }: { score: LeadScore }) {
  const tier = score.tier.toLowerCase();

  return (
    <section className="drawer-section">
      <div className="section-title-row">
        <h3>Szansa na zlecenie</h3>
        <span className={`score-headline score-text-${tier}`}>
          {score.value}/100 · {scoreTierLabel[score.tier]}
        </span>
      </div>

      <div className="score-track" aria-hidden="true">
        <div className={`score-fill score-fill-${tier}`} style={{ width: `${score.value}%` }} />
      </div>

      <ul className="score-factors">
        {score.factors.map((factor) => (
          <li key={factor.label}>
            <span className={`score-points ${factor.points > 0 ? 'plus' : factor.points < 0 ? 'minus' : 'zero'}`}>
              {factor.points > 0 ? `+${factor.points}` : factor.points}
            </span>
            <span>{factor.label}</span>
          </li>
        ))}
      </ul>

      <p className="hint">
        Szacunek z jawnych sygnałów (stan strony, ruch w salonie, branża), a nie pewność. Traktuj go jako podpowiedź,
        od kogo zacząć.
      </p>
    </section>
  );
}
