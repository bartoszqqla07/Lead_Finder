import { useEffect, useRef, useState } from 'react';
import { addDaysIso, todayIso } from '../filters';
import { formatDateTime, formatRating, plural, stages } from '../labels';
import type { Lead, LeadChanges, MessageDraft, OutreachStage } from '../types';
import { StatusBadge } from './Badges';
import { OutreachPanel } from './OutreachPanel';
import { ScoreBreakdown } from './ScoreBreakdown';
import { PreviewStudio } from './preview/PreviewStudio';
import { useLatest } from '../hooks/useLatest';

interface Props {
  lead: Lead;
  onClose: () => void;
  onUpdate: (id: number, changes: LeadChanges) => Promise<void>;
  onDelete: (id: number, block: boolean) => Promise<void>;
  onError: (message: string) => void;
}

const NOTES_SAVE_DELAY_MS = 700;

/** "Dziś", "Jutro", "Zaległe: 3 dni" albo "Za 5 dni" – czytelniej niż sama data. */
function DueHint({ date }: { date: string }) {
  const diffDays = Math.round((Date.parse(date) - Date.parse(todayIso())) / 86_400_000);
  const text =
    diffDays < 0
      ? `Zaległe: ${-diffDays} ${plural(-diffDays, 'dzień', 'dni', 'dni')}`
      : diffDays === 0
        ? 'Na dziś'
        : diffDays === 1
          ? 'Jutro'
          : `Za ${diffDays} dni`;
  return <p className={`hint ${diffDays <= 0 ? 'due-now' : ''}`}>{text}</p>;
}
const today = () =>
  new Intl.DateTimeFormat('pl-PL', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date());

export function LeadDrawer({ lead, onClose, onUpdate, onDelete, onError }: Props) {
  const [notes, setNotes] = useState(lead.notes);
  const [notesState, setNotesState] = useState<'saved' | 'dirty' | 'saving'>('saved');
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [studioOpen, setStudioOpen] = useState(false);
  // Escape w otwartym kreatorze zamyka tylko kreator, nie cały panel leada.
  const studioOpenRef = useLatest(studioOpen);
  const closeRef = useRef<HTMLButtonElement>(null);
  // Callbacki z App zmieniają tożsamość przy każdym renderze (np. przy zdarzeniach SSE),
  // więc efekty czytają je z refów – inaczej debounce notatek i fokus resetowałyby się w kółko.
  const callbacks = useLatest({ onUpdate, onError, onClose });

  // Autozapis notatek z opóźnieniem – bez przycisku "Zapisz".
  useEffect(() => {
    if (notes === lead.notes) return;
    setNotesState('dirty');
    const timer = setTimeout(() => {
      setNotesState('saving');
      callbacks.current
        .onUpdate(lead.id, { notes })
        .then(() => setNotesState('saved'))
        .catch((e: Error) => {
          setNotesState('dirty');
          callbacks.current.onError(e.message);
        });
    }, NOTES_SAVE_DELAY_MS);
    return () => clearTimeout(timer);
  }, [notes, lead.id, lead.notes, callbacks]);

  useEffect(() => {
    closeRef.current?.focus();
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && !studioOpenRef.current && callbacks.current.onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [callbacks, studioOpenRef]);

  const setStage = (stage: OutreachStage) => {
    onUpdate(lead.id, { stage }).catch((e: Error) => onError(e.message));
  };

  const setConsent = (given: boolean) => {
    onUpdate(lead.id, { consentGiven: given }).catch((e: Error) => onError(e.message));
  };

  const setNextAction = (date: string | null) => {
    onUpdate(lead.id, date ? { nextActionDate: date } : { clearNextAction: true }).catch((e: Error) =>
      onError(e.message),
    );
  };

  /**
   * Dopisuje do notatek datę i kanał (historia kontaktu) i przesuwa etap z "Nowy" na "Skontaktowany".
   * Po wysłaniu propozycji ustawia przypomnienie za tydzień – na jedno dozwolone przypomnienie.
   */
  const markSent = (draft: MessageDraft) => {
    const entry = `${today()} – wysłano: ${draft.title}`;
    const updatedNotes = notes ? `${entry}\n${notes}` : entry;
    setNotes(updatedNotes);
    onUpdate(lead.id, {
      notes: updatedNotes,
      stage: lead.stage === 'New' ? 'Contacted' : undefined,
      // Po ofercie (krótkiej albo pełnej) – przypomnienie za tydzień, na jedno dozwolone "Przypomnienie".
      nextActionDate: draft.kind === 'Proposal' || draft.kind === 'OfferShort' ? addDaysIso(7) : undefined,
    }).catch((e: Error) => onError(e.message));
  };

  return (
    <>
      <div className="overlay" onClick={onClose} />
      <aside className="drawer" role="dialog" aria-modal="true" aria-labelledby="drawer-title">
        <header className="drawer-header">
          <div>
            <h2 id="drawer-title">{lead.name}</h2>
            <p className="muted">
              {lead.categoryName} · {lead.city}
            </p>
          </div>
          <button ref={closeRef} className="icon-button" onClick={onClose} aria-label="Zamknij">
            ×
          </button>
        </header>

        <div className="drawer-body">
          <section className="drawer-section">
            <div className="status-line">
              <StatusBadge status={lead.status} />
              {lead.rating !== null && (
                <span className="muted">
                  <span className="star">★</span> {formatRating(lead.rating)} · {lead.userRatingCount ?? 0} opinii
                </span>
              )}
            </div>
            {lead.checkNote && <p className="check-note">{lead.checkNote}</p>}
          </section>

          <ScoreBreakdown score={lead.score} />

          <section className="drawer-section">
            <h3>Kontakt</h3>
            <dl className="details">
              <dt>Telefon</dt>
              <dd>{lead.phone ? <a href={`tel:${lead.phone.replace(/\s/g, '')}`}>{lead.phone}</a> : '—'}</dd>
              <dt>Adres</dt>
              <dd>
                {lead.address ?? '—'}{' '}
                <a href={lead.googleMapsUrl} target="_blank" rel="noreferrer">
                  mapa ↗
                </a>
              </dd>
              <dt>Strona</dt>
              <dd className="break">
                {lead.websiteUri ? (
                  <a href={lead.websiteUri} target="_blank" rel="noreferrer">
                    {lead.websiteUri}
                  </a>
                ) : (
                  '—'
                )}
              </dd>
            </dl>
          </section>

          <OutreachPanel
            lead={lead}
            onConsentChange={setConsent}
            onMarkSent={markSent}
            onOpenStudio={() => setStudioOpen(true)}
            onError={onError}
          />

          <section className="drawer-section">
            <h3>Etap</h3>
            <div className="stage-picker" role="radiogroup" aria-label="Etap kontaktu">
              {stages.map((stage) => (
                <button
                  key={stage.value}
                  role="radio"
                  aria-checked={lead.stage === stage.value}
                  className={`stage-option stage-${stage.value.toLowerCase()} ${lead.stage === stage.value ? 'active' : ''}`}
                  onClick={() => setStage(stage.value)}
                >
                  {stage.label}
                </button>
              ))}
            </div>
            {lead.stageChangedAt && <p className="hint">Zmieniono {formatDateTime(lead.stageChangedAt)}</p>}

            <div className="next-action">
              <span className="field-label">Następny krok</span>
              <div className="next-action-row">
                <input
                  className="input next-action-date"
                  type="date"
                  value={lead.nextActionDate ?? ''}
                  onChange={(e) => setNextAction(e.target.value || null)}
                  aria-label="Data następnego kroku"
                />
                <button className="button button-small" onClick={() => setNextAction(addDaysIso(1))}>
                  Jutro
                </button>
                <button className="button button-small" onClick={() => setNextAction(addDaysIso(3))}>
                  Za 3 dni
                </button>
                <button className="button button-small" onClick={() => setNextAction(addDaysIso(7))}>
                  Za tydzień
                </button>
                {lead.nextActionDate && (
                  <button className="link-button small" onClick={() => setNextAction(null)}>
                    usuń
                  </button>
                )}
              </div>
              {lead.nextActionDate && <DueHint date={lead.nextActionDate} />}
            </div>
          </section>

          <section className="drawer-section">
            <div className="section-title-row">
              <h3>Notatki</h3>
              <span className="save-state">
                {notesState === 'saving' ? 'Zapisuję…' : notesState === 'dirty' ? '' : notes ? 'Zapisano ✓' : ''}
              </span>
            </div>
            <textarea
              className="input textarea"
              rows={4}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Np. z kim rozmawiać, kiedy oddzwonić, co ich interesuje…"
            />
          </section>
        </div>

        <footer className="drawer-footer">
          {confirmDelete ? (
            <span className="confirm">
              <button
                className="button button-small button-danger"
                onClick={() => void onDelete(lead.id, true)}
                title="Firma nie chce kontaktu: usuwa dane i nie pokaże jej przy kolejnych wyszukiwaniach"
              >
                Usuń i nie pokazuj więcej
              </button>
              <button className="button button-small" onClick={() => void onDelete(lead.id, false)}>
                Tylko usuń
              </button>
              <button className="button button-small button-ghost" onClick={() => setConfirmDelete(false)}>
                Anuluj
              </button>
            </span>
          ) : (
            <span className="muted">Dodano {formatDateTime(lead.firstSeenAt)}</span>
          )}
          {!confirmDelete && (
            <button className="link-button danger" onClick={() => setConfirmDelete(true)}>
              Usuń lead
            </button>
          )}
        </footer>
      </aside>

      {studioOpen && <PreviewStudio lead={lead} onClose={() => setStudioOpen(false)} onError={onError} />}
    </>
  );
}
