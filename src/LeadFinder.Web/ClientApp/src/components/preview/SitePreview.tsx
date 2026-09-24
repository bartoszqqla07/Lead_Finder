import type { CSSProperties } from 'react';
import type { PreviewData } from './presets';

/**
 * Makieta strony głównej salonu. Ten sam komponent wyświetla się w ramce laptopa (1280 px)
 * i telefonu (390 px) – układ przełącza się przez container queries, a nie media queries,
 * bo obie wersje są na ekranie jednocześnie.
 */
export function SitePreview({ data }: { data: PreviewData }) {
  const [hero, ...rest] = data.photos;
  const gallery = rest.slice(0, 3);

  return (
    <div className={`sp sp-${data.style}`} style={{ '--sp-accent': data.accent } as CSSProperties}>
      <header className="sp-nav">
        <span className="sp-logo">{data.name}</span>
        <nav className="sp-links">
          <span>Oferta</span>
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

      <section className={`sp-hero ${hero ? 'sp-hero-photo' : ''}`}>
        {hero && <img className="sp-hero-img" src={hero} alt="" />}
        <div className="sp-hero-inner">
          {data.badge && <p className="sp-badge">{data.badge}</p>}
          <h1>{data.name}</h1>
          <p className="sp-tagline">{data.tagline}</p>
          <div className="sp-cta">
            <span className="sp-btn">Zarezerwuj wizytę</span>
            {data.phone && <span className="sp-btn sp-btn-ghost">Zadzwoń</span>}
          </div>
        </div>
      </section>

      <section className="sp-section">
        <h2>Cennik</h2>
        <ul className="sp-services">
          {data.services.map((service) => (
            <li key={service.name}>
              <span>{service.name}</span>
              <span className="sp-dots" aria-hidden="true" />
              <strong>{service.price}</strong>
            </li>
          ))}
        </ul>
      </section>

      {gallery.length > 0 && (
        <section className="sp-section">
          <h2>Nasze prace</h2>
          <div className="sp-gallery">
            {gallery.map((photo) => (
              <img key={photo} src={photo} alt="" />
            ))}
          </div>
        </section>
      )}

      <footer className="sp-footer">
        <div>
          <h3>Kontakt</h3>
          <p>{data.address}</p>
          {data.phone && <p>{data.phone}</p>}
        </div>
        <div>
          <h3>Godziny otwarcia</h3>
          <p>{data.hours}</p>
        </div>
      </footer>
    </div>
  );
}
