import { scoreTierLabel } from '../labels';
import type { LeadScore } from '../types';

/**
 * Szansa na zlecenie: pasek 0–100 widoczny od razu, składniki punktów po rozwinięciu.
 * <details> siedzi w osobnej sekcji, bo jako kontener flex źle się układa w Chromium.
 */
export function ScoreBreakdown({ score }: { score: LeadScore }) {
  const tier = score.tier.toLowerCase();

  return (
    <section className="drawer-section">
      <details className="score-details">
        <summary>
          <span className="section-title-row">
            <h3>Szansa na zlecenie</h3>
            <span className={`score-headline score-text-${tier}`}>
              {score.value}/100 · {scoreTierLabel[score.tier]}
            </span>
          </span>
          <span className="score-track" aria-hidden="true">
            <span className={`score-fill score-fill-${tier}`} style={{ width: `${score.value}%` }} />
          </span>
          <span className="collapsible-hint">Dlaczego tyle?</span>
        </summary>

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
          Szacunek z jawnych sygnałów (stan strony, ruch w salonie, branża), a nie pewność. Traktuj go jako
          podpowiedź, od kogo zacząć.
        </p>
      </details>
    </section>
  );
}
