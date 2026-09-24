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

const googleSearch = (query: string) => `https://www.google.com/search?q=${encodeURIComponent(query)}`;

export function OutreachPanel({ lead, onConsentChange, onMarkSent, onOpenStudio, onError }: Props) {
  const [kind, setKind] = useState<DraftKind>(() => recommendedKind(lead));
  const [copied, setCopied] = useState<'body' | 'subject' | null>(null);
  const draft = lead.drafts.find((d) => d.kind === kind) ?? lead.drafts[0];
  const hasConsent = lead.consentGivenAt !== null;

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

  const firstContact = lead.drafts.filter((d) => !d.requiresConsent);
  const afterConsent = lead.drafts.filter((d) => d.requiresConsent);
  const locked = draft.requiresConsent && !hasConsent;

  return (
    <section className="drawer-section">
      <div className="section-title-row">
        <h3>Kontakt zdalny</h3>
        <button className="link-button small" onClick={onOpenStudio}>
          🎨 Kreator podglądu strony
        </button>
      </div>

      <div className="contact-finder">
        <span className="muted">Znajdź kontakt:</span>
        <a href={googleSearch(`site:instagram.com "${lead.name}" ${lead.city}`)} target="_blank" rel="noreferrer">
          Instagram ↗
        </a>
        <a href={googleSearch(`site:facebook.com "${lead.name}" ${lead.city}`)} target="_blank" rel="noreferrer">
          Facebook ↗
        </a>
        <a href={googleSearch(`"${lead.name}" ${lead.city} e-mail kontakt`)} target="_blank" rel="noreferrer">
          e-mail ↗
        </a>
        {lead.profilePlatform && lead.websiteUri && (
          <a href={lead.websiteUri} target="_blank" rel="noreferrer">
            profil {lead.profilePlatform} ↗
          </a>
        )}
      </div>

      <label className={`consent ${hasConsent ? 'consent-on' : ''}`}>
        <input
          type="checkbox"
          checked={hasConsent}
          onChange={(e) => {
            onConsentChange(e.target.checked);
            setKind(e.target.checked ? 'Preview' : recommendedKind({ ...lead, consentGivenAt: null }));
          }}
        />
        <span>
          <strong>Firma zgodziła się na przesłanie oferty</strong>
          <span className="muted">
            {hasConsent
              ? ` · ${formatDateTime(lead.consentGivenAt!)} – zachowaj też zrzut odpowiedzi jako dowód`
              : ' · zaznacz, gdy odpiszą „tak” – odblokuje propozycję'}
          </span>
        </span>
      </label>

      <div className="draft-tabs" role="tablist" aria-label="Rodzaj wiadomości">
        <span className="draft-group-label">Pierwszy kontakt</span>
        {firstContact.map((d) => (
          <DraftTab
            key={d.kind}
            draft={d}
            active={d.kind === draft.kind}
            recommended={d.kind === recommendedKind(lead)}
            onSelect={setKind}
          />
        ))}
        <span className="draft-group-label">Po zgodzie</span>
        {afterConsent.map((d) => (
          <DraftTab
            key={d.kind}
            draft={d}
            active={d.kind === draft.kind}
            recommended={d.kind === recommendedKind(lead)}
            locked={!hasConsent}
            onSelect={setKind}
          />
        ))}
      </div>

      <div className="draft-card">
        <p className="draft-channel">
          <strong>Gdzie:</strong> {draft.channel}
        </p>
        <p className={`guidance ${locked ? 'guidance-warning' : ''}`}>
          {locked && <strong>Najpierw uzyskaj zgodę. </strong>}
          {draft.guidance}
        </p>

        {draft.subject && (
          <div className="subject-row">
            <span className="muted">Temat:</span>
            <span className="subject">{draft.subject}</span>
            <button className="link-button small" onClick={() => void copy(draft.subject!, 'subject')}>
              {copied === 'subject' ? 'skopiowano ✓' : 'kopiuj'}
            </button>
          </div>
        )}

        {draft.kind === 'Preview' && (
          <button className="button button-primary studio-cta" onClick={onOpenStudio}>
            🎨 Stwórz podgląd strony dla tego salonu
          </button>
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
          <button className="button button-small" onClick={() => onMarkSent(draft)} disabled={locked}>
            Oznacz jako wysłane
          </button>
        </div>
      </div>
    </section>
  );
}

function DraftTab({
  draft,
  active,
  recommended,
  locked = false,
  onSelect,
}: {
  draft: MessageDraft;
  active: boolean;
  recommended: boolean;
  locked?: boolean;
  onSelect: (kind: DraftKind) => void;
}) {
  return (
    <button
      role="tab"
      aria-selected={active}
      className={`draft-tab ${active ? 'active' : ''} ${locked ? 'locked' : ''}`}
      onClick={() => onSelect(draft.kind)}
      title={locked ? 'Wymaga zgody firmy' : undefined}
    >
      {locked && <span aria-hidden="true">🔒 </span>}
      {draft.title}
      {recommended && <span className="recommended">polecane</span>}
    </button>
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
