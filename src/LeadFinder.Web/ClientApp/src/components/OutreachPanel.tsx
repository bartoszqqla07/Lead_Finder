import { useState } from 'react';
import { formatDateTime } from '../labels';
import type { DraftKind, Lead, MessageDraft } from '../types';

interface Props {
  lead: Lead;
  onConsentChange: (given: boolean) => void;
  onMarkSent: (draft: MessageDraft) => void;
  onOpenStudio: () => void;
  onError: (message: string) => void;
}

/**
 * Który szkic pokazać na start: po zgodzie – podgląd (makieta); przy niedziałającej stronie – sama informacja
 * o problemie; w pozostałych przypadkach – DM na Instagramie (tam salony odpowiadają najszybciej).
 */
function recommendedKind(lead: Lead): DraftKind {
  if (lead.consentGivenAt) return 'Preview';
  if (lead.drafts.some((d) => d.kind === 'ProblemNotice') && lead.stage === 'New') return 'ProblemNotice';
  return 'DirectMessage';
}

/**
 * Wiadomość do salonu – pokazuje tylko bieżący krok: przed zgodą prośby o zgodę, po zgodzie podgląd i ofertę.
 * Wskazówki są zwinięte, a zgodę zaznacza się jednym przyciskiem pod wiadomością.
 */
export function OutreachPanel({ lead, onConsentChange, onMarkSent, onOpenStudio, onError }: Props) {
  const [kind, setKind] = useState<DraftKind>(() => recommendedKind(lead));
  const [copied, setCopied] = useState<'body' | 'subject' | null>(null);
  const hasConsent = lead.consentGivenAt !== null;
  const stepDrafts = lead.drafts.filter((d) => d.requiresConsent === hasConsent);
  const draft = stepDrafts.find((d) => d.kind === kind) ?? stepDrafts[0];
  const recommended = recommendedKind(lead);

  if (!draft) return null;

  const copy = async (text: string, what: 'body' | 'subject') => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(what);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      onError('Nie udało się skopiować – zaznacz tekst ręcznie.');
    }
  };

  const changeConsent = (given: boolean) => {
    onConsentChange(given);
    setKind(given ? 'Preview' : recommendedKind({ ...lead, consentGivenAt: null }));
  };

  return (
    <section className="drawer-section">
      <div className="section-title-row">
        <h3>Wiadomość</h3>
        <span className={`step-pill ${hasConsent ? 'step-pill-done' : ''}`}>
          {hasConsent ? 'Krok 2 · po zgodzie' : 'Krok 1 · prośba o zgodę'}
        </span>
      </div>

      <div className="draft-tabs" role="tablist" aria-label="Rodzaj wiadomości">
        {stepDrafts.map((d) => (
          <button
            key={d.kind}
            role="tab"
            aria-selected={d.kind === draft.kind}
            className={`draft-tab ${d.kind === draft.kind ? 'active' : ''}`}
            onClick={() => setKind(d.kind)}
          >
            {d.title}
            {d.kind === recommended && (
              <span className="recommended-dot" title="Polecane na teraz" aria-label="polecane" />
            )}
          </button>
        ))}
      </div>

      <details className="draft-tips">
        <summary>
          <strong>Gdzie:</strong> {draft.channel}
          <span className="collapsible-hint">wskazówki</span>
        </summary>
        <p>{draft.guidance}</p>
      </details>

      {draft.kind === 'Preview' && (
        <button className="button button-primary studio-cta" onClick={onOpenStudio}>
          🎨 Stwórz podgląd strony dla tego salonu
        </button>
      )}

      {draft.subject && (
        <div className="subject-row">
          <span className="muted">Temat:</span>
          <span className="subject">{draft.subject}</span>
          <button className="link-button small" onClick={() => void copy(draft.subject!, 'subject')}>
            {copied === 'subject' ? 'skopiowano ✓' : 'kopiuj'}
          </button>
        </div>
      )}

      <pre className="draft">{draft.body}</pre>

      <div className="button-row">
        <button className="button button-small button-primary" onClick={() => void copy(draft.body, 'body')}>
          {copied === 'body' ? 'Skopiowano ✓' : 'Kopiuj treść'}
        </button>
        {draft.kind === 'Letter' && (
          <button className="button button-small" onClick={() => printLetter(draft.body, onError)}>
            Drukuj list
          </button>
        )}
        <button className="button button-small" onClick={() => onMarkSent(draft)}>
          Oznacz jako wysłane
        </button>
      </div>

      {hasConsent ? (
        <p className="consent-status" title="Zachowaj też zrzut ekranu odpowiedzi jako dowód zgody">
          <span className="consent-check">✓</span>
          Zgoda od {formatDateTime(lead.consentGivenAt!)}
          <button className="link-button small" onClick={() => changeConsent(false)}>
            cofnij
          </button>
        </p>
      ) : (
        <div className="consent-cta">
          <span className="muted">Odpisali, że mogą dostać podgląd?</span>
          <button className="button button-small button-ok" onClick={() => changeConsent(true)}>
            ✓ Mam zgodę – dalej
          </button>
        </div>
      )}
    </section>
  );
}

/** Otwiera list w nowym oknie z prostym układem do druku (A4, czcionka szeryfowa). */
function printLetter(body: string, onError: (message: string) => void) {
  const printWindow = window.open('', '_blank', 'width=800,height=1000');
  if (!printWindow) {
    onError('Przeglądarka zablokowała okno drukowania – zezwól na wyskakujące okna dla localhost.');
    return;
  }

  const escaped = body.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  printWindow.document.write(`<!doctype html><html lang="pl"><head><meta charset="utf-8"><title>List</title>
<style>
  @page { size: A4; margin: 22mm 20mm; }
  body { font: 11.5pt/1.55 Georgia, 'Times New Roman', serif; color: #111; margin: 0; }
  pre { white-space: pre-wrap; font: inherit; margin: 0; }
</style></head><body><pre>${escaped}</pre></body></html>`);
  printWindow.document.close();
  printWindow.focus();
  printWindow.print();
}
