import { useRef, useState } from 'react';
import type { Lead, LeadChanges } from '../types';

interface Props {
  lead: Lead;
  onSave: (contacts: NonNullable<LeadChanges['contacts']>) => Promise<void>;
  onError: (message: string) => void;
}

type Field = 'email' | 'instagramUrl' | 'facebookUrl';

const googleSearch = (query: string) => `https://www.google.com/search?q=${encodeURIComponent(query)}`;

/** "booksy.com" zamiast długiego adresu profilu – pełny adres jest w podpowiedzi i pod linkiem. */
export function shortUrl(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www[.]/, '');
  } catch {
    return url;
  }
}

/**
 * Sekcja "Kontakt" w panelu leada. Automat znajduje e-mail i profile tylko tam, gdzie są publiczne (strona salonu,
 * Booksy); resztę uzupełniasz raz: "szukaj ↗" otwiera dokładne wyszukiwanie, a "dodaj" – pole do wklejenia.
 */
export function ContactsSection({ lead, onSave, onError }: Props) {
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [values, setValues] = useState({ email: '', instagramUrl: '', facebookUrl: '' });
  const inputs = useRef<Partial<Record<Field, HTMLInputElement | null>>>({});

  // Nazwa bez miasta – "Est Clinic Katowice" szukamy jako "Est Clinic" + Katowice.
  const searchName = lead.name.replace(new RegExp(`\\s*${escapeRegExp(lead.city)}\\s*$`, 'i'), '').trim() || lead.name;
  const searches: Record<Field, string> = {
    email: googleSearch(`"${searchName}" ${lead.city} kontakt e-mail`),
    instagramUrl: googleSearch(`site:instagram.com "${searchName}" ${lead.city}`),
    facebookUrl: googleSearch(`site:facebook.com "${searchName}" ${lead.city}`),
  };

  const startEditing = (focus?: Field) => {
    setValues({
      email: lead.emails[0] ?? '',
      instagramUrl: lead.instagramUrl ?? '',
      facebookUrl: lead.facebookUrl ?? '',
    });
    setEditing(true);
    if (focus) setTimeout(() => inputs.current[focus]?.focus(), 0);
  };

  const save = async () => {
    setSaving(true);
    try {
      await onSave(values);
      setEditing(false);
    } catch (e) {
      onError((e as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const missing = (field: Field) => (
    <span className="contact-missing">
      <span className="muted">brak · </span>
      <a href={searches[field]} target="_blank" rel="noreferrer">
        szukaj ↗
      </a>
      <span className="muted"> · </span>
      <button type="button" className="link-button small" onClick={() => startEditing(field)}>
        dodaj
      </button>
    </span>
  );

  return (
    <section className="drawer-section">
      <div className="section-title-row">
        <h3>Kontakt</h3>
        {!editing && (
          <span className="section-title-actions">
            {lead.contactsEditedByUser && <span className="muted small-text">poprawione ręcznie</span>}
            <button type="button" className="link-button small" onClick={() => startEditing()}>
              ✏️ Popraw kontakty
            </button>
          </span>
        )}
      </div>

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
        <dt>{lead.profilePlatform ? 'Profil' : 'Strona'}</dt>
        <dd>
          {lead.websiteUri ? (
            <a href={lead.websiteUri} target="_blank" rel="noreferrer" title={lead.websiteUri}>
              {shortUrl(lead.websiteUri)} ↗
            </a>
          ) : (
            '—'
          )}
        </dd>

        {!editing && (
          <>
            <dt>E-mail</dt>
            <dd>
              {lead.emails.length > 0 ? (
                <span className="contact-list">
                  {lead.emails.map((email) => (
                    <span key={email} className="contact-item">
                      <a href={`mailto:${email}`}>{email}</a>
                      <CopyButton text={email} onError={onError} />
                    </span>
                  ))}
                </span>
              ) : (
                missing('email')
              )}
            </dd>
            <dt>Instagram</dt>
            <dd>{lead.instagramUrl ? <ProfileLink url={lead.instagramUrl} /> : missing('instagramUrl')}</dd>
            <dt>Facebook</dt>
            <dd>{lead.facebookUrl ? <ProfileLink url={lead.facebookUrl} /> : missing('facebookUrl')}</dd>
          </>
        )}
      </dl>

      {editing && (
        <form
          className="contacts-form"
          onSubmit={(e) => {
            e.preventDefault();
            void save();
          }}
        >
          {(
            [
              ['email', 'E-mail', 'kontakt@salon.pl'],
              ['instagramUrl', 'Instagram', '@salon albo link do profilu'],
              ['facebookUrl', 'Facebook', 'link do strony salonu na Facebooku'],
            ] as const
          ).map(([field, label, placeholder]) => (
            <label key={field} className="field">
              <span className="field-label-row">
                <span className="field-label">{label}</span>
                <a href={searches[field]} target="_blank" rel="noreferrer" className="link-button small">
                  szukaj ↗
                </a>
              </span>
              <input
                ref={(el) => {
                  inputs.current[field] = el;
                }}
                className="input"
                value={values[field]}
                placeholder={placeholder}
                onChange={(e) => setValues((v) => ({ ...v, [field]: e.target.value }))}
              />
            </label>
          ))}
          <p className="hint">
            Wklej, co znalazłeś – aplikacja sama poprawi format. Ręcznie poprawione kontakty nie są nadpisywane przy
            ponownym sprawdzaniu stron.
          </p>
          <div className="button-row">
            <button type="submit" className="button button-small button-primary" disabled={saving}>
              {saving ? 'Zapisuję…' : 'Zapisz kontakty'}
            </button>
            <button type="button" className="button button-small" onClick={() => setEditing(false)} disabled={saving}>
              Anuluj
            </button>
          </div>
        </form>
      )}
    </section>
  );
}

/** Link do profilu salonu jako "@nazwa ↗". */
function ProfileLink({ url }: { url: string }) {
  const handle = profileHandle(url);
  return (
    <a href={url} target="_blank" rel="noreferrer" title={url}>
      {handle ? `@${handle}` : shortUrl(url)} ↗
    </a>
  );
}

function profileHandle(url: string): string | null {
  try {
    const path = new URL(url).pathname.split('/').filter(Boolean);
    return path[0] && path[0] !== 'profile.php' ? path[0] : null;
  } catch {
    return null;
  }
}

function CopyButton({ text, onError }: { text: string; onError: (message: string) => void }) {
  const [copied, setCopied] = useState(false);
  return (
    <button
      type="button"
      className="link-button small"
      onClick={() =>
        navigator.clipboard.writeText(text).then(
          () => {
            setCopied(true);
            setTimeout(() => setCopied(false), 1500);
          },
          () => onError('Nie udało się skopiować – zaznacz tekst ręcznie.'),
        )
      }
    >
      {copied ? 'skopiowano ✓' : 'kopiuj'}
    </button>
  );
}

const escapeRegExp = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
