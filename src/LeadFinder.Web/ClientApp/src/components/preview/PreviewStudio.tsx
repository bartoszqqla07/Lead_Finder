import { toBlob, toPng } from 'html-to-image';
import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties, type Ref } from 'react';
import { useLatest } from '../../hooks/useLatest';
import type { Lead } from '../../types';
import {
  featuresToText,
  initialPreviewData,
  servicesToText,
  textToFeatures,
  textToServices,
  type HeroLayout, type PreviewData, type PreviewStyle } from './presets';
import { SitePreview } from './SitePreview';
import './preview.css';

interface Props {
  lead: Lead;
  onClose: () => void;
  onError: (message: string) => void;
}

type Layout = 'combo' | 'desktop' | 'phone' | 'phones';

const LAYOUTS: { value: Layout; label: string }[] = [
  { value: 'combo', label: 'Laptop + telefon' },
  { value: 'desktop', label: 'Laptop' },
  { value: 'phone', label: 'Telefon' },
  { value: 'phones', label: '2 telefony' },
];

const HEROES: { value: HeroLayout; label: string }[] = [
  { value: 'overlay', label: 'Zdjęcie w tle' },
  { value: 'split', label: 'Obok zdjęcia' },
  { value: 'centered', label: 'Wyśrodkowany' },
];

const STYLES: { value: PreviewStyle; label: string }[] = [
  { value: 'dark', label: 'Ciemny' },
  { value: 'light', label: 'Jasny' },
  { value: 'pastel', label: 'Pastelowy' },
];

const SWATCHES = ['#c8a165', '#b08968', '#c48b8b', '#d9779f', '#7d8f6e', '#5f93a8', '#d64545', '#4f46e5'];

/** Szerokość płótna w pikselach – eksport ma zawsze ten sam rozmiar, niezależnie od okna. */
const CANVAS_SIZE: Record<Layout, { width: number; height: number }> = {
  combo: { width: 1000, height: 620 },
  desktop: { width: 1000, height: 720 },
  phone: { width: 520, height: 900 },
  phones: { width: 880, height: 900 },
};

/** Tekstowe pola makiety zapamiętane per lead (bez zdjęć – te są tylko w pamięci przeglądarki). */
const storageKey = (leadId: number) => `leadfinder.preview.v2.${leadId}`;

// Bez cacheBust: doklejany "?czas" psuje adresy blob: wgranych zdjęć i eksport kończy się błędem.
const EXPORT_OPTIONS = { pixelRatio: 2 } as const;

/**
 * Kreator podglądu strony: makieta strony głównej salonu (jego nazwa, zdjęcia, cennik) w ramce laptopa
 * i telefonu – do wysłania jako obrazek po tym, jak salon zgodził się na propozycję.
 */
export function PreviewStudio({ lead, onClose, onError }: Props) {
  const [data, setData] = useState<PreviewData>(() => loadSaved(lead) ?? initialPreviewData(lead));
  const [servicesText, setServicesText] = useState(() => servicesToText(data.services));
  const [featuresText, setFeaturesText] = useState(() => featuresToText(data.features));
  const [layout, setLayout] = useState<Layout>('combo');
  const [busy, setBusy] = useState<'download' | 'copy' | null>(null);
  const [copied, setCopied] = useState(false);
  const canvasRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<HTMLDivElement>(null);
  const [stageScale, setStageScale] = useState(1);

  const set = <K extends keyof PreviewData>(key: K, value: PreviewData[K]) => setData((d) => ({ ...d, [key]: value }));

  // Zapamiętaj teksty (nazwa, hasło, cennik…) – po ponownym otwarciu kreator wróci do tej samej makiety.
  useEffect(() => {
    try {
      const texts: Partial<PreviewData> = { ...data };
      delete texts.photos;
      localStorage.setItem(storageKey(lead.id), JSON.stringify(texts));
    } catch {
      /* brak localStorage – trudno, makieta po prostu nie zostanie zapamiętana */
    }
  }, [data, lead.id]);

  // Zdjęcia to adresy blob: – zwalniamy je przy zamknięciu kreatora.
  const photosRef = useLatest(data.photos);
  useEffect(() => () => photosRef.current.forEach((url) => URL.revokeObjectURL(url)), [photosRef]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  // Płótno ma stały rozmiar (do eksportu) – na ekranie skalujemy je, żeby zmieściło się w oknie.
  useLayoutEffect(() => {
    const stage = stageRef.current;
    if (!stage) return;
    const update = () => {
      const byWidth = (stage.clientWidth - 32) / CANVAS_SIZE[layout].width;
      // Na szerokim ekranie scena ma własny scroll i stałą wysokość – mieścimy płótno też na wysokość
      // (pasek narzędzi + podpowiedź + odstępy ≈ 120 px). Na wąskim scena rośnie z treścią, więc tylko szerokość.
      const fixedHeight = getComputedStyle(stage).overflowY === 'auto';
      const byHeight = fixedHeight ? (stage.clientHeight - 120) / CANVAS_SIZE[layout].height : Infinity;
      setStageScale(Math.max(0.2, Math.min(1, byWidth, byHeight)));
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(stage);
    return () => observer.disconnect();
  }, [layout]);

  const addPhotos = (files: FileList | null) => {
    if (!files) return;
    const urls = [...files].filter((f) => f.type.startsWith('image/')).map((f) => URL.createObjectURL(f));
    set('photos', [...data.photos, ...urls].slice(0, 4));
  };

  const removePhoto = (url: string) => {
    URL.revokeObjectURL(url);
    set(
      'photos',
      data.photos.filter((p) => p !== url),
    );
  };

  const makePhotoMain = (url: string) => set('photos', [url, ...data.photos.filter((p) => p !== url)]);

  const download = async () => {
    if (!canvasRef.current) return;
    setBusy('download');
    try {
      const dataUrl = await toPng(canvasRef.current, EXPORT_OPTIONS);
      const link = Object.assign(document.createElement('a'), {
        href: dataUrl,
        download: `podglad-${slug(data.name)}.png`,
      });
      link.click();
    } catch (e) {
      onError(`Nie udało się zapisać obrazka${e instanceof Error ? `: ${e.message}` : ' – spróbuj usunąć i dodać zdjęcia ponownie.'}`);
    } finally {
      setBusy(null);
    }
  };

  const copyImage = async () => {
    if (!canvasRef.current) return;
    setBusy('copy');
    try {
      const blob = await toBlob(canvasRef.current, EXPORT_OPTIONS);
      if (!blob) throw new Error('pusty obraz');
      await navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })]);
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    } catch {
      onError('Przeglądarka nie pozwoliła skopiować obrazka – użyj „Pobierz PNG”.');
    } finally {
      setBusy(null);
    }
  };

  return (
    <>
      <div className="overlay" onClick={onClose} />
      <div className="studio" role="dialog" aria-modal="true" aria-labelledby="studio-title">
        <header className="drawer-header">
          <div>
            <h2 id="studio-title">Kreator podglądu strony</h2>
            <p className="muted">{lead.name}</p>
          </div>
          <button className="icon-button" onClick={onClose} aria-label="Zamknij">
            ×
          </button>
        </header>

        <div className="studio-body">
          <aside className="studio-form">
            <label className="field">
              <span className="field-label">Nazwa na stronie</span>
              <input className="input" value={data.name} onChange={(e) => set('name', e.target.value)} />
              <span className="hint">Skróć oficjalną nazwę z Google do tej, której używa salon.</span>
            </label>

            <label className="field">
              <span className="field-label">Hasło pod nazwą</span>
              <input className="input" value={data.tagline} onChange={(e) => set('tagline', e.target.value)} />
            </label>

            <label className="field">
              <span className="field-label">Podpis nad nazwą</span>
              <input
                className="input"
                value={data.eyebrow}
                onChange={(e) => set('eyebrow', e.target.value)}
                placeholder="np. Barbershop · Katowice"
              />
            </label>

            <div className="field-grid">
              <label className="field">
                <span className="field-label">Ocena Google</span>
                <input
                  className="input"
                  value={data.rating}
                  onChange={(e) => set('rating', e.target.value)}
                  placeholder="np. 4,9"
                />
              </label>
              <label className="field">
                <span className="field-label">Liczba opinii</span>
                <input
                  className="input"
                  value={data.reviews}
                  onChange={(e) => set('reviews', e.target.value)}
                  placeholder="np. 312"
                />
              </label>
            </div>
            <span className="hint studio-hint-tight">Puste pole oceny = bez karty z opiniami.</span>

            <div className="field">
              <span className="field-label">Styl</span>
              <div className="segmented" role="radiogroup" aria-label="Styl makiety">
                {STYLES.map((s) => (
                  <button
                    key={s.value}
                    type="button"
                    role="radio"
                    aria-checked={data.style === s.value}
                    className={data.style === s.value ? 'active' : ''}
                    onClick={() => set('style', s.value)}
                  >
                    {s.label}
                  </button>
                ))}
              </div>
            </div>

            <div className="field">
              <span className="field-label">Układ strony</span>
              <div className="segmented" role="radiogroup" aria-label="Układ nagłówka strony">
                {HEROES.map((h) => (
                  <button
                    key={h.value}
                    type="button"
                    role="radio"
                    aria-checked={data.hero === h.value}
                    className={data.hero === h.value ? 'active' : ''}
                    onClick={() => set('hero', h.value)}
                  >
                    {h.label}
                  </button>
                ))}
              </div>
            </div>

            <div className="field">
              <span className="field-label">Kolor przewodni</span>
              <div className="swatches">
                {SWATCHES.map((color) => (
                  <button
                    key={color}
                    type="button"
                    className={`swatch ${data.accent === color ? 'active' : ''}`}
                    style={{ background: color }}
                    onClick={() => set('accent', color)}
                    aria-label={`Kolor ${color}`}
                  />
                ))}
                <input
                  type="color"
                  className="swatch-picker"
                  value={data.accent}
                  onChange={(e) => set('accent', e.target.value)}
                  aria-label="Własny kolor"
                />
              </div>
              <span className="hint">Dobierz do logo albo zdjęć salonu.</span>
            </div>

            <div className="field">
              <span className="field-label">Zdjęcia (do 4, pierwsze = główne)</span>
              <div className="photo-grid">
                {data.photos.map((url, index) => (
                  <div key={url} className={`photo-thumb ${index === 0 ? 'main' : ''}`}>
                    <img src={url} alt="" />
                    <div className="photo-actions">
                      {index > 0 && (
                        <button type="button" onClick={() => makePhotoMain(url)} title="Ustaw jako główne">
                          ★
                        </button>
                      )}
                      <button type="button" onClick={() => removePhoto(url)} title="Usuń">
                        ×
                      </button>
                    </div>
                  </div>
                ))}
                {data.photos.length < 4 && (
                  <label className="photo-add">
                    <input type="file" accept="image/*" multiple onChange={(e) => addPhotos(e.target.files)} />+ dodaj
                  </label>
                )}
              </div>
              {data.photos.length === 0 ? (
                <span className="hint warning">
                  Zdjęcia robią największe wrażenie – zapisz 3–4 z Instagrama salonu (wnętrze, efekty pracy) i wgraj
                  tutaj. Salon od razu rozpozna swoje miejsce.
                </span>
              ) : (
                <span className="hint">
                  Pierwsze to tło nagłówka, kolejne trafiają do galerii. Makieta jest tylko do wysłania im prywatnie.
                </span>
              )}
            </div>

            <label className="field">
              <span className="field-label">Cennik (Usługa | cena)</span>
              <textarea
                className="input textarea"
                rows={5}
                value={servicesText}
                onChange={(e) => {
                  setServicesText(e.target.value);
                  set('services', textToServices(e.target.value));
                }}
              />
              <span className="hint warning">
                Ceny są przykładowe – wpisz prawdziwe z ich Booksy/Instagrama albo usuń linie.
              </span>
            </label>

            <label className="field">
              <span className="field-label">Atuty (tytuł | opis, max 3)</span>
              <textarea
                className="input textarea"
                rows={3}
                value={featuresText}
                onChange={(e) => {
                  setFeaturesText(e.target.value);
                  set('features', textToFeatures(e.target.value));
                }}
              />
              <span className="hint">Jeśli salon czymś się wyróżnia (np. na Instagramie), wpisz to tutaj.</span>
            </label>

            <label className="field">
              <span className="field-label">Hasło w pasku rezerwacji</span>
              <input className="input" value={data.ctaTitle} onChange={(e) => set('ctaTitle', e.target.value)} />
            </label>

            <label className="field">
              <span className="field-label">Adres</span>
              <input className="input" value={data.address} onChange={(e) => set('address', e.target.value)} />
            </label>
            <div className="field-grid">
              <label className="field">
                <span className="field-label">Telefon</span>
                <input className="input" value={data.phone} onChange={(e) => set('phone', e.target.value)} />
              </label>
              <label className="field">
                <span className="field-label">Godziny</span>
                <input className="input" value={data.hours} onChange={(e) => set('hours', e.target.value)} />
              </label>
            </div>

            <button
              type="button"
              className="link-button small"
              onClick={() => {
                const fresh = { ...initialPreviewData(lead), photos: data.photos };
                setData(fresh);
                setServicesText(servicesToText(fresh.services));
                setFeaturesText(featuresToText(fresh.features));
              }}
            >
              Przywróć ustawienia startowe
            </button>
          </aside>

          <section className="studio-stage" ref={stageRef}>
            <div className="studio-toolbar">
              <div className="segmented segmented-4" role="radiogroup" aria-label="Układ obrazka">
                {LAYOUTS.map((l) => (
                  <button
                    key={l.value}
                    type="button"
                    role="radio"
                    aria-checked={layout === l.value}
                    className={layout === l.value ? 'active' : ''}
                    onClick={() => setLayout(l.value)}
                  >
                    {l.label}
                  </button>
                ))}
              </div>
              <div className="button-row">
                <button className="button button-small" onClick={() => void copyImage()} disabled={busy !== null}>
                  {copied ? 'Skopiowano ✓' : busy === 'copy' ? 'Kopiuję…' : 'Kopiuj obraz'}
                </button>
                <button
                  className="button button-small button-primary"
                  onClick={() => void download()}
                  disabled={busy !== null}
                >
                  {busy === 'download' ? 'Zapisuję…' : 'Pobierz PNG'}
                </button>
              </div>
            </div>

            <div className="studio-canvas-holder" style={{ height: CANVAS_SIZE[layout].height * stageScale }}>
              <div style={{ transform: `scale(${stageScale})`, transformOrigin: 'top left' }}>
                <PreviewCanvas ref={canvasRef} data={data} layout={layout} />
              </div>
            </div>
            <p className="hint">
              „Kopiuj obraz” i wklej (Ctrl+V) prosto w wiadomość na Instagramie lub Messengerze w przeglądarce.
            </p>
          </section>
        </div>
      </div>
    </>
  );
}

/** Płótno eksportu: tło w kolorze salonu + ramki urządzeń z makietą w środku. */
function PreviewCanvas({ data, layout, ref }: { data: PreviewData; layout: Layout; ref: Ref<HTMLDivElement> }) {
  return (
    <div
      ref={ref}
      className={`pv-canvas pv-layout-${layout} pv-bg-${data.style}`}
      style={{ '--sp-accent': data.accent, ...CANVAS_SIZE[layout] } as CSSProperties}
    >
      {(layout === 'combo' || layout === 'desktop') && (
        <div className="pv-laptop">
          <div className="pv-laptop-bar">
            <i />
            <i />
            <i />
          </div>
          <div className="pv-laptop-screen">
            <div className="pv-scale pv-scale-desktop">
              <SitePreview data={data} />
            </div>
          </div>
        </div>
      )}
      {layout !== 'desktop' && <PhoneFrame data={data} from="top" />}
      {layout === 'phones' && <PhoneFrame data={data} from="services" />}
    </div>
  );
}

function PhoneFrame({ data, from }: { data: PreviewData; from: 'top' | 'services' }) {
  return (
    <div className="pv-phone">
      <div className="pv-phone-screen">
        <div className="pv-scale pv-scale-phone">
          <SitePreview data={data} from={from} />
        </div>
      </div>
    </div>
  );
}

function loadSaved(lead: Lead): PreviewData | null {
  try {
    const saved = localStorage.getItem(storageKey(lead.id));
    return saved ? { ...initialPreviewData(lead), ...(JSON.parse(saved) as Partial<PreviewData>), photos: [] } : null;
  } catch {
    return null;
  }
}

const slug = (text: string) =>
  text
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .replace(/ł/g, 'l')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 40) || 'salon';
