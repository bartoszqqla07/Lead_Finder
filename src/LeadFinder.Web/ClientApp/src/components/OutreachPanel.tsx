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
 * o problemie; gdy znamy e-mail salonu – e-mail (na DM-y salony często nie odpisują); w pozostałych – DM.
 */
function recommendedKind(lead: Lead): DraftKind {
  if (lead.consentGivenAt) return 'Preview';
  if (lead.drafts.some((d) => d.kind === 'ProblemNotice') && lead.stage === 'New') return 'ProblemNotice';
  if (lead.emails.length > 0) return 'Email';
  return 'DirectMessage';
}

/** Link otwierający program pocztowy z gotowym adresem, tematem i treścią – wysyła się ręcznie. */
const mailtoUrl = (to: string, subject: string, body: string) =>
  `mailto:${encodeURIComponent(to).replace(/%40/g, '@')}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;

/**
 * Wiadomość do salonu – pokazuje tylko bieżący krok: przed zgodą prośby o zgodę, po zgodzie podgląd i ofertę.
 * Wskazówki są zwinięte, a zgodę zaznacza się jednym przyciskiem pod wiadomością.
 */
export function OutreachPanel({ lead, onConsentChange, onMarkSent, onOpenStudio, onError }: Props) {
  const [kind, setKind] = useState<DraftKind>(() => recommendedKind(lead));
  const [copied, setCopied] = useState<'body' | 'subject' | null>(null);
  const [chosenSubject, setChosenSubject] = useState<string | null>(null);
  const hasConsent = lead.consentGivenAt !== null;
  const stepDrafts = lead.drafts.filter((d) => d.requiresConsent === hasConsent);
  const draft = stepDrafts.find((d) => d.kind === kind) ?? stepDrafts[0];
  const recommended = recommendedKind(lead);

  if (!draft) return null;

  // Wybrany temat obowiązuje tylko dla szkicu, który go oferuje; inaczej domyślny temat szkicu.
  const subject =
    chosenSubject && draft.subjectOptions?.includes(chosenSubject) ? chosenSubject : draft.subject;

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
        <span className="section-title-actions">
          <button className="link-button small" onClick={onOpenStudio}>
            🎨 Kreator podglądu
          </button>
          <span className={`step-pill ${hasConsent ? 'step-pill-done' : ''}`}>
            {hasConsent ? 'Krok 2 · po zgodzie' : 'Krok 1 · prośba o zgodę'}
          </span>
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
      {/* DM mówi „przygotowałem podgląd” – więc robimy go przed wysłaniem, żeby po „tak” wysłać od razu. */}
      {draft.kind === 'DirectMessage' && (
        <button className="button studio-cta" onClick={onOpenStudio}>
          🎨 Najpierw przygotuj podgląd (kreator)
        </button>
      )}

      {subject && (
        <div className="subject-block">
          <div className="subject-row">
            <span className="muted">Temat:</span>
            <span className="subject">{subject}</span>
            <button className="link-button small" onClick={() => void copy(subject, 'subject')}>
              {copied === 'subject' ? 'skopiowano ✓' : 'kopiuj'}
            </button>
          </div>
          {draft.subjectOptions && draft.subjectOptions.length > 1 && (
            <div className="subject-options" role="radiogroup" aria-label="Temat wiadomości">
              {draft.subjectOptions.map((option) => (
                <button
                  key={option}
                  type="button"
                  role="radio"
                  aria-checked={option === subject}
                  className={`subject-option ${option === subject ? 'active' : ''}`}
                  onClick={() => setChosenSubject(option)}
                >
                  {option}
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {draft.kind === 'Email' && (
        <p className={`email-to ${lead.emails.length > 0 ? '' : 'muted'}`}>
          {lead.emails.length > 0 ? (
            <>
              <span className="muted">Do:</span> <strong>{lead.emails[0]}</strong>
              {lead.emails.length > 1 && <span className="muted"> (inne adresy w sekcji Kontakt)</span>}
            </>
          ) : (
            'Brak e-maila – znajdź go przez „szukaj ↗” w sekcji Kontakt i kliknij „dodaj”, albo napisz DM-a.'
          )}
        </p>
      )}

      <pre className="draft">{draft.body}</pre>

      <div className="button-row">
        {draft.kind === 'Email' && (
          <a
            className="button button-small button-primary"
            href={mailtoUrl(lead.emails[0] ?? '', subject ?? '', draft.body)}
            title="Otwiera Twój program pocztowy z gotowym adresem, tematem i treścią – wysyłasz sam"
          >
            ✉️ Otwórz w poczcie
          </a>
        )}
        {/* Messenger strony firmowej trafia do skrzynki w Meta Business Suite – salony czytają ją częściej niż
            "Prośby o wiadomość" na Instagramie (tam lądują DM-y od kont, których nie obserwują). */}
        {draft.kind === 'DirectMessage' && lead.facebookUrl && (
          <a className="button button-small button-primary" href={lead.facebookUrl} target="_blank" rel="noreferrer">
            💬 Otwórz Facebook salonu
          </a>
        )}
        {draft.kind === 'DirectMessage' && lead.instagramUrl && (
          <a
            className={`button button-small ${lead.facebookUrl ? '' : 'button-primary'}`}
            href={lead.instagramUrl}
            target="_blank"
            rel="noreferrer"
          >
            📷 Otwórz Instagram salonu
          </a>
        )}
        <button
          className={`button button-small ${draft.kind === 'Email' || (draft.kind === 'DirectMessage' && (lead.instagramUrl || lead.facebookUrl)) ? '' : 'button-primary'}`}
          onClick={() => void copy(draft.body, 'body')}
        >
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
