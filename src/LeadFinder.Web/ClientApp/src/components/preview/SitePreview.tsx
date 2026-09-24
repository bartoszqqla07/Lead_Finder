import type { CSSProperties } from 'react';
import './fonts.css';
import './site.css';
import { reviewsLabel, type IconName, type PreviewData } from './presets';

interface Props {
  data: PreviewData;
  /** "services" pokazuje stronę przewiniętą do cennika – drugi telefon w układzie "2 telefony". */
  from?: 'top' | 'services';
}

/**
 * Makieta strony głównej salonu. Ten sam komponent wyświetla się w ramce laptopa (1280 px)
 * i telefonu (390 px) – układ przełącza się przez container queries, a nie media queries,
 * bo obie wersje są na ekranie jednocześnie.
 */
export function SitePreview({ data, from = 'top' }: Props) {
  const [hero, ...rest] = data.photos;
  const gallery = rest.slice(0, 3);
  const split = data.hero === 'split';
  // W układzie "split" zdjęcie stoi obok tekstu; w pozostałych jest tłem pod przyciemnieniem.
  const photoBackground = hero !== undefined && !split;
  // Najdłuższe słowo nazwy – CSS dobiera wielkość nagłówka tak, żeby zmieściło się w jednej linii.
  const longestWord = Math.max(1, ...data.name.split(' ').map((word) => word.length));
  const rating = data.rating.trim() ? <RatingCard rating={data.rating} reviews={data.reviews} /> : null;

  return (
    <div className={`sp sp-${data.style}`} style={{ '--sp-accent': data.accent, '--sp-word': longestWord } as CSSProperties}>
      <header className="sp-nav">
        <span className="sp-logo">{data.name}</span>
        <nav className="sp-links">
          <span>O nas</span>
          <span>Cennik</span>
          {gallery.length > 0 && <span>Galeria</span>}
          <span>Kontakt</span>
        </nav>
        <span className="sp-btn sp-btn-small sp-nav-cta">Zarezerwuj</span>
        <span className="sp-burger" aria-hidden="true">
          <i />
          <i />
          <i />
        </span>
      </header>

      {from === 'top' && (
        <>
          <section className={`sp-hero sp-hero-${data.hero} ${photoBackground ? 'sp-hero-photo' : ''}`}>
            {photoBackground && <img className="sp-hero-img" src={hero} alt="" />}
            {!photoBackground && !split && <div className="sp-hero-art" aria-hidden="true" />}
            <div className="sp-hero-inner">
              {data.eyebrow && <p className="sp-eyebrow">{data.eyebrow}</p>}
              <h1>{data.name}</h1>
              <p className="sp-tagline">{data.tagline}</p>
              <div className="sp-cta">
                <span className="sp-btn">Zarezerwuj wizytę</span>
                {data.phone && <span className="sp-btn sp-btn-ghost">Zadzwoń</span>}
              </div>
              {!split && rating}
            </div>
            {split && (
              <div className="sp-hero-media">
                <div className="sp-hero-frame">
                  {hero ? (
                    <img src={hero} alt="" />
                  ) : (
                    <span className="sp-monogram" aria-hidden="true">
                      {data.name.trim().charAt(0).toUpperCase()}
                    </span>
                  )}
                </div>
                {rest[0] && <img className="sp-hero-small" src={rest[0]} alt="" />}
                {rating}
              </div>
            )}
          </section>

          {data.features.length > 0 && (
            <section className="sp-features">
              {data.features.map((feature, index) => (
                <div key={`${feature.title}-${index}`} className="sp-feature">
                  <span className="sp-feature-icon">
                    <Icon name={data.icons[index % data.icons.length] ?? 'star'} />
                  </span>
                  <div>
                    <strong>{feature.title}</strong>
                    {feature.text && <span>{feature.text}</span>}
                  </div>
                </div>
              ))}
            </section>
          )}
        </>
      )}

      <section className="sp-section sp-pricing">
        <p className="sp-kicker">Usługi</p>
        <h2>Cennik</h2>
        <ul className="sp-services">
          {data.services.map((service, index) => (
            <li key={`${service.name}-${index}`}>
              <span>{service.name}</span>
              <span className="sp-dots" aria-hidden="true" />
              <strong>{service.price}</strong>
            </li>
          ))}
        </ul>
      </section>

      {gallery.length > 0 && (
        <section className="sp-section sp-gallery-section">
          <p className="sp-kicker">Galeria</p>
          <h2>Nasze prace</h2>
          <div className={`sp-gallery sp-gallery-${gallery.length}`}>
            {gallery.map((photo) => (
              <img key={photo} src={photo} alt="" />
            ))}
          </div>
        </section>
      )}

      <section className="sp-band">
        <h2>{data.ctaTitle}</h2>
        <span className="sp-btn sp-btn-inverse">Zarezerwuj online</span>
      </section>

      <section className="sp-section sp-contact">
        <div>
          <p className="sp-kicker">Kontakt</p>
          <h2>Zapraszamy</h2>
          <ul className="sp-contact-list">
            <li>
              <Icon name="pin" />
              {data.address}
            </li>
            {data.phone && (
              <li>
                <Icon name="phone" />
                {data.phone}
              </li>
            )}
            <li>
              <Icon name="clock" />
              {data.hours}
            </li>
          </ul>
        </div>
        <div className="sp-map" aria-hidden="true">
          <span className="sp-map-pin">
            <Icon name="pin" />
          </span>
        </div>
      </section>

      <footer className="sp-footer">
        <span>© {new Date().getFullYear()} {data.name}</span>
        <span>Rezerwacja online 24/7</span>
      </footer>
    </div>
  );
}

function RatingCard({ rating, reviews }: { rating: string; reviews: string }) {
  return (
    <div className="sp-rating">
      <strong>{rating}</strong>
      <div>
        <span className="sp-stars" aria-hidden="true">
          ★★★★★
        </span>
        <span>{reviewsLabel(reviews)}</span>
      </div>
    </div>
  );
}

/** Proste ikony liniowe – rysowane w SVG, żeby wyglądały ostro w eksporcie i nie wymagały biblioteki. */
const ICON_PATHS: Record<IconName | 'phone', string[]> = {
  clock: ['M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z', 'M12 7v5l3.5 2'],
  scissors: [
    'M6 3.5a2.5 2.5 0 1 0 0 5a2.5 2.5 0 1 0 0-5z',
    'M6 15.5a2.5 2.5 0 1 0 0 5a2.5 2.5 0 1 0 0-5z',
    'M20 4L8.1 15.9',
    'M14.5 14.5L20 20',
    'M8.1 8.1L12 12',
  ],
  pin: ['M12 21s-7-6.2-7-11a7 7 0 0 1 14 0c0 4.8-7 11-7 11z', 'M12 7.5a2.5 2.5 0 1 0 0 5a2.5 2.5 0 1 0 0-5z'],
  star: ['M12 3l2.6 5.6 6.1.7-4.5 4.2 1.2 6L12 16.6l-5.4 2.9 1.2-6-4.5-4.2 6.1-.7z'],
  sparkle: ['M11 3l1.8 5.2L18 10l-5.2 1.8L11 17l-1.8-5.2L4 10l5.2-1.8z', 'M18.5 14.5l.7 2 2 .7-2 .7-.7 2-.7-2-2-.7 2-.7z'],
  leaf: ['M5 19C5 11 10 5 20 5c0 10-6 15-13 15-1.2 0-2-.3-2-1z', 'M5 19l8-8'],
  heart: ['M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.2a4.3 4.3 0 0 1 7.5 2.6C19.5 15.4 12 20 12 20z'],
  shield: ['M12 3l7 3v5.5c0 4.6-3.2 8-7 9.5-3.8-1.5-7-4.9-7-9.5V6z', 'M9 12l2.2 2.2L15.5 10'],
  drop: ['M12 3.5s6 6.4 6 10.8a6 6 0 0 1-12 0C6 9.9 12 3.5 12 3.5z'],
  pen: ['M4 20l4.2-1L19 8.2 15.8 5 5 15.8z', 'M13.8 7l3.2 3.2'],
  phone: [
    'M5 4h3.5l1.5 4-2 1.5a11 11 0 0 0 6.5 6.5l1.5-2 4 1.5V19a1.5 1.5 0 0 1-1.6 1.5C10.6 20 4 13.4 3.5 5.6A1.5 1.5 0 0 1 5 4z',
  ],
};

function Icon({ name }: { name: IconName | 'phone' }) {
  return (
    <svg className="sp-icon" viewBox="0 0 24 24" aria-hidden="true">
      {ICON_PATHS[name].map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  );
}
