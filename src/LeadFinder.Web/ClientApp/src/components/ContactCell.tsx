import { useState } from 'react';
import { websiteHost } from '../labels';
import type { Lead } from '../types';

const googleSearch = (query: string) => `https://www.google.com/search?q=${encodeURIComponent(query)}`;

/**
 * Kolumna "E-mail" na liście: adres do skopiowania jednym kliknięciem, a pod nim linki do strony (albo Booksy),
 * Instagrama i Facebooka salonu. Gdy nic nie znamy – szybkie wyszukiwanie. Kliknięcia nie otwierają panelu leada.
 */
export function ContactCell({ lead }: { lead: Lead }) {
  const [copied, setCopied] = useState(false);
  const email = lead.emails[0];
  const host = websiteHost(lead);
  const stop = (e: React.MouseEvent) => e.stopPropagation();

  const copy = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (!email) return;
    void navigator.clipboard.writeText(email).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  };

  const links = [
    lead.websiteUri && host && { href: lead.websiteUri, label: lead.profilePlatform ?? host },
    lead.instagramUrl && lead.profilePlatform !== 'Instagram' && { href: lead.instagramUrl, label: 'Instagram' },
    lead.facebookUrl && lead.profilePlatform !== 'Facebook' && { href: lead.facebookUrl, label: 'Facebook' },
  ].filter((link): link is { href: string; label: string } => Boolean(link));

  return (
    <>
      {email ? (
        <div className="contact-email">
          <a href={`mailto:${email}`} onClick={stop} title={lead.emails.join(', ')}>
            {email}
          </a>
          <button type="button" className="copy-chip" onClick={copy} aria-label={`Kopiuj ${email}`}>
            {copied ? '✓' : 'kopiuj'}
          </button>
        </div>
      ) : (
        <span className="muted">—</span>
      )}
      <div className="cell-sub contact-socials">
        {links.map((link) => (
          <a key={link.label} href={link.href} target="_blank" rel="noreferrer" onClick={stop} className="truncate">
            {link.label}
          </a>
        ))}
        {!email && !lead.instagramUrl && (
          <a
            href={googleSearch(`site:instagram.com "${lead.name}" ${lead.city}`)}
            target="_blank"
            rel="noreferrer"
            onClick={stop}
            className="muted-link"
          >
            szukaj ↗
          </a>
        )}
      </div>
    </>
  );
}
