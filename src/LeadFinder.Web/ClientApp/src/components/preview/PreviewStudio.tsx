import { toBlob, toPng } from 'html-to-image';
import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode, type Ref } from 'react';
import type { Lead } from '../../types';
import { randomComposition } from './compositions';
import {
  featuresToText,
  initialPreviewData,
  isUploadedPhoto,
  servicesToText,
  STOCK_TONES,
  stockPhotos,
  textToFeatures,
  textToServices,
  type ButtonShape,
  type FontPair,
  type HeroLayout,
  type PreviewData,
  type PreviewSections,
  type PreviewStyle,
} from './presets';
import { SitePreview } from './SitePreview';
import './preview.css';

interface Props {
  lead: Lead;
  onClose: () => void;
  onError: (message: string) => void;
}

type Layout = 'combo' | 'desktop' | 'phone' | 'phones';
type Tab = 'look' | 'photos' | 'texts' | 'sections';

interface Option<T> {
  value: T;
  label: string;
}

const LAYOUTS: Option<Layout>[] = [
  { value: 'combo', label: 'Laptop + telefon' },
  { value: 'desktop', label: 'Laptop' },
  { value: 'phone', label: 'Telefon' },
  { value: 'phones', label: '2 telefony' },
];

const TABS: Option<Tab>[] = [
  { value: 'look', label: 'Wygląd' },
  { value: 'photos', label: 'Zdjęcia' },
  { value: 'texts', label: 'Teksty' },
  { value: 'sections', label: 'Sekcje' },
];

const STYLES: Option<PreviewStyle>[] = [
  { value: 'dark', label: 'Ciemny' },
  { value: 'light', label: 'Jasny' },
  { value: 'pastel', label: 'Pastelowy' },
];

const FONTS: Option<FontPair>[] = [
  { value: 'bold', label: 'Mocna' },
  { value: 'elegant', label: 'Elegancka' },
  { value: 'soft', label: 'Miękka' },
  { value: 'clean', label: 'Nowoczesna' },
];

const HEROES: Option<HeroLayout>[] = [
  { value: 'overlay', label: 'Zdjęcie w tle' },
  { value: 'split', label: 'Obok zdjęcia' },
  { value: 'centered', label: 'Wyśrodkowany' },
];

const BUTTONS: Option<ButtonShape>[] = [
  { value: 'sharp', label: 'Kanciaste' },
  { value: 'rounded', label: 'Zaokrąglone' },
  { value: 'pill', label: 'Owalne' },
];

const SECTIONS: Option<keyof PreviewSections>[] = [
  { value: 'features', label: 'Atuty pod nagłówkiem' },
  { value: 'services', label: 'Cennik' },
  { value: 'gallery', label: 'Galeria (zdjęcia 2–4)' },
  { value: 'band', label: 'Pasek rezerwacji' },
  { value: 'contact', label: 'Kontakt z mapką' },
];

const SWATCHES = ['#c8a165', '#b08968', '#c48b8b', '#d9779f', '#7d8f6e', '#5f93a8', '#d64545', '#4f46e5'];

const MAX_PHOTOS = 4;
const HISTORY_LIMIT = 15;

/** Rozmiar płótna w pikselach – eksport ma zawsze ten sam rozmiar, niezależnie od okna. */
const CANVAS_SIZE: Record<Layout, { width: number; height: number }> = {
  combo: { width: 1000, height: 620 },
  desktop: { width: 1000, height: 720 },
  phone: { width: 520, height: 900 },
  phones: { width: 880, height: 900 },
};

/** Makieta zapamiętana per lead – bez wgranych zdjęć (blob:), które żyją tylko do zamknięcia kreatora. */
const storageKey = (leadId: number) => `leadfinder.preview.v2.${leadId}`;

// Bez cacheBust: doklejany "?czas" psuje adresy blob: wgranych zdjęć i eksport kończy się błędem.
const EXPORT_OPTIONS = { pixelRatio: 2 } as const;

/**
 * Kreator podglądu strony: makieta strony głównej salonu (jego nazwa, zdjęcia, cennik) w ramce laptopa
 * i telefonu – do wysłania jako obrazek. "Losuj" podsuwa kompozycje dopasowane do branży salonu.
 */
export function PreviewStudio({ lead, onClose, onError }: Props) {
  const [data, setData] = useState<PreviewData>(() => loadSaved(lead) ?? initialPreviewData(lead));
  const [servicesText, setServicesText] = useState(() => servicesToText(data.services));
  const [featuresText, setFeaturesText] = useState(() => featuresToText(data.features));
  const [history, setHistory] = useState<PreviewData[]>([]);
  const [tab, setTab] = useState<Tab>('look');
  const [stockTone, setStockTone] = useState(lead.categoryTone);
  const [layout, setLayout] = useState<Layout>('combo');
  const [busy, setBusy] = useState<'download' | 'copy' | null>(null);
  const [copied, setCopied] = useState(false);
  const canvasRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<HTMLDivElement>(null);
  const [stageScale, setStageScale] = useState(1);
  // Wszystkie utworzone adresy blob: – zwalniane dopiero przy zamknięciu, bo "cofnij" może przywrócić usunięte zdjęcie.
  const uploadedUrls = useRef(new Set<string>());

  const set = <K extends keyof PreviewData>(key: K, value: PreviewData[K]) => setData((d) => ({ ...d, [key]: value }));

  /** Podmienia całą makietę (losowanie, cofnięcie, reset) razem z polami tekstowymi list. */
  const replaceData = (next: PreviewData) => {
    setData(next);
    setServicesText(servicesToText(next.services));
    setFeaturesText(featuresToText(next.features));
  };

  const shuffleLook = () => {
    setHistory((h) => [...h.slice(-(HISTORY_LIMIT - 1)), data]);
    replaceData(randomComposition(data, lead.categoryTone, lead.city));
  };

  const undo = () => {
    const previous = history.at(-1);
    if (!previous) return;
    setHistory((h) => h.slice(0, -1));
    replaceData(previous);
  };

  // Zapamiętaj makietę – po ponownym otwarciu kreator wróci do tej samej wersji (bez wgranych zdjęć).
  useEffect(() => {
    try {
      const saved: PreviewData = { ...data, photos: data.photos.filter((p) => !isUploadedPhoto(p)) };
      localStorage.setItem(storageKey(lead.id), JSON.stringify(saved));
    } catch {
      /* brak localStorage – trudno, makieta po prostu nie zostanie zapamiętana */
    }
  }, [data, lead.id]);

  useEffect(() => {
    const urls = uploadedUrls.current;
    return () => urls.forEach((url) => URL.revokeObjectURL(url));
  }, []);

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
    urls.forEach((url) => uploadedUrls.current.add(url));
    // Zdjęcia salonu mają pierwszeństwo przed gotowymi – wypychają je z listy, a nie odwrotnie.
    const uploaded = data.photos.filter(isUploadedPhoto);
    const stock = data.photos.filter((p) => !isUploadedPhoto(p));
    set('photos', [...uploaded, ...urls, ...stock].slice(0, MAX_PHOTOS));
  };

  const removePhoto = (url: string) =>
    set(
      'photos',
      data.photos.filter((p) => p !== url),
    );

  const makePhotoMain = (url: string) => set('photos', [url, ...data.photos.filter((p) => p !== url)]);

  const toggleStockPhoto = (url: string) => {
    if (data.photos.includes(url)) removePhoto(url);
    else if (data.photos.length < MAX_PHOTOS) set('photos', [...data.photos, url]);
    else onError(`Makieta ma najwyżej ${MAX_PHOTOS} zdjęcia – najpierw usuń któreś z wybranych.`);
  };

  const insertStockSet = () => {
    const uploaded = data.photos.filter(isUploadedPhoto);
    set('photos', [...uploaded, ...stockPhotos(stockTone)].slice(0, MAX_PHOTOS));
  };

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
      onError(
        `Nie udało się zapisać obrazka${e instanceof Error ? `: ${e.message}` : ' – spróbuj usunąć i dodać zdjęcia ponownie.'}`,
      );
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

  const hasUploaded = data.photos.some(isUploadedPhoto);

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
            <div className="studio-dice">
              <button type="button" className="button button-primary" onClick={shuffleLook}>
                🎲 Losuj kompozycję
              </button>
              <button type="button" className="button" onClick={undo} disabled={history.length === 0}>
                ↶ Cofnij
              </button>
            </div>
            <p className="hint studio-dice-hint">
              Styl, kolory, czcionki i hasła dobrane do branży: {lead.categoryName}. Nazwa, cennik, kontakt i Twoje
              zdjęcia zostają.
            </p>

            <Segmented options={TABS} value={tab} onChange={setTab} label="Część kreatora" className="studio-tabs" />

            {tab === 'look' && (
              <>
                <Field label="Kolorystyka">
                  <Segmented options={STYLES} value={data.style} onChange={(v) => set('style', v)} label="Kolorystyka" />
                </Field>
                <Field label="Czcionki">
                  <Segmented
                    options={FONTS}
                    value={data.font}
                    onChange={(v) => set('font', v)}
                    label="Czcionki"
                    className="segmented-4"
                  />
                </Field>
                <Field label="Układ nagłówka">
                  <Segmented options={HEROES} value={data.hero} onChange={(v) => set('hero', v)} label="Układ nagłówka" />
                </Field>
                <Field label="Przyciski">
                  <Segmented options={BUTTONS} value={data.buttons} onChange={(v) => set('buttons', v)} label="Przyciski" />
                </Field>
                <Field label="Kolor przewodni" hint="Dobierz do logo albo zdjęć salonu.">
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
                </Field>
                <Field label={`Przyciemnienie zdjęcia: ${Math.round(data.overlay * 100)}%`} hint="Jaśniej = więcej zdjęcia, ciemniej = czytelniejszy tekst.">
                  <input
                    type="range"
                    className="range"
                    min={0.3}
                    max={1}
                    step={0.05}
                    value={data.overlay}
                    onChange={(e) => set('overlay', Number(e.target.value))}
                    aria-label="Przyciemnienie zdjęcia"
                  />
                </Field>
              </>
            )}

            {tab === 'photos' && (
              <>
                <Field label={`Na makiecie (${data.photos.length}/${MAX_PHOTOS}) – pierwsze to tło nagłówka`}>
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
                    {data.photos.length < MAX_PHOTOS && (
                      <label className="photo-add">
                        <input type="file" accept="image/*" multiple onChange={(e) => addPhotos(e.target.files)} />+
                        wgraj
                      </label>
                    )}
                  </div>
                </Field>
                <p className={`hint ${hasUploaded ? '' : 'warning'}`}>
                  {hasUploaded
                    ? 'Zdjęcia salonu robią największe wrażenie – dobrze, że są. Wgrane zdjęcia nie zapisują się po zamknięciu kreatora.'
                    : 'Na razie są zdjęcia przykładowe. Najlepiej wgraj 3–4 zdjęcia z Instagrama salonu – od razu rozpozna swoje miejsce.'}
                </p>

                <Field label="Gotowe zdjęcia">
                  <div className="stock-toolbar">
                    <select
                      className="input select"
                      value={stockTone}
                      onChange={(e) => setStockTone(e.target.value)}
                      aria-label="Branża gotowych zdjęć"
                    >
                      {STOCK_TONES.map((t) => (
                        <option key={t.value} value={t.value}>
                          {t.label}
                        </option>
                      ))}
                    </select>
                    <button type="button" className="button button-small" onClick={insertStockSet}>
                      Wstaw zestaw
                    </button>
                  </div>
                  <div className="stock-grid">
                    {stockPhotos(stockTone).map((url) => (
                      <button
                        key={url}
                        type="button"
                        className={`stock-thumb ${data.photos.includes(url) ? 'active' : ''}`}
                        onClick={() => toggleStockPhoto(url)}
                        title={data.photos.includes(url) ? 'Usuń z makiety' : 'Dodaj do makiety'}
                      >
                        <img src={url} alt="" loading="lazy" />
                      </button>
                    ))}
                  </div>
                  <span className="hint">Kliknij, żeby dodać albo usunąć. Zdjęcia z Unsplash – można ich używać za darmo.</span>
                </Field>
              </>
            )}

            {tab === 'texts' && (
              <>
                <Field label="Nazwa na stronie" hint="Skróć oficjalną nazwę z Google do tej, której używa salon.">
                  <input className="input" value={data.name} onChange={(e) => set('name', e.target.value)} />
                </Field>
                <Field label="Podpis nad nazwą">
                  <input
                    className="input"
                    value={data.eyebrow}
                    onChange={(e) => set('eyebrow', e.target.value)}
                    placeholder="np. Barbershop · Katowice"
                  />
                </Field>
                <Field label="Hasło pod nazwą">
                  <input className="input" value={data.tagline} onChange={(e) => set('tagline', e.target.value)} />
                </Field>
                <Field label="Tekst głównego przycisku">
                  <input
                    className="input"
                    value={data.ctaLabel}
                    onChange={(e) => set('ctaLabel', e.target.value)}
                    placeholder="Zarezerwuj wizytę"
                  />
                </Field>
                <div className="field-grid">
                  <Field label="Ocena Google">
                    <input
                      className="input"
                      value={data.rating}
                      onChange={(e) => set('rating', e.target.value)}
                      placeholder="np. 4,9"
                    />
                  </Field>
                  <Field label="Liczba opinii">
                    <input
                      className="input"
                      value={data.reviews}
                      onChange={(e) => set('reviews', e.target.value)}
                      placeholder="np. 312"
                    />
                  </Field>
                </div>
                <span className="hint studio-hint-tight">Puste pole oceny = bez karty z opiniami.</span>
                <Field label="Hasło w pasku rezerwacji">
                  <input className="input" value={data.ctaTitle} onChange={(e) => set('ctaTitle', e.target.value)} />
                </Field>
              </>
            )}

            {tab === 'sections' && (
              <>
                <Field label="Widoczne sekcje">
                  <div className="section-toggles">
                    {SECTIONS.map((s) => (
                      <label key={s.value} className="checkbox-row">
                        <input
                          type="checkbox"
                          checked={data.sections[s.value]}
                          onChange={(e) => set('sections', { ...data.sections, [s.value]: e.target.checked })}
                        />
                        {s.label}
                      </label>
                    ))}
                  </div>
                </Field>
                <Field
                  label="Cennik (usługa | cena)"
                  hint="Ceny są przykładowe – wpisz prawdziwe z ich Booksy/Instagrama albo usuń linie."
                  hintWarning
                >
                  <textarea
                    className="input textarea"
                    rows={5}
                    value={servicesText}
                    onChange={(e) => {
                      setServicesText(e.target.value);
                      set('services', textToServices(e.target.value));
                    }}
                  />
                </Field>
                <Field label="Atuty (tytuł | opis, max 3)" hint="Jeśli salon czymś się wyróżnia (np. na Instagramie), wpisz to tutaj.">
                  <textarea
                    className="input textarea"
                    rows={3}
                    value={featuresText}
                    onChange={(e) => {
                      setFeaturesText(e.target.value);
                      set('features', textToFeatures(e.target.value));
                    }}
                  />
                </Field>
                <Field label="Adres">
                  <input className="input" value={data.address} onChange={(e) => set('address', e.target.value)} />
                </Field>
                <div className="field-grid">
                  <Field label="Telefon">
                    <input className="input" value={data.phone} onChange={(e) => set('phone', e.target.value)} />
                  </Field>
                  <Field label="Godziny">
                    <input className="input" value={data.hours} onChange={(e) => set('hours', e.target.value)} />
                  </Field>
                </div>
              </>
            )}

            <button
              type="button"
              className="link-button small"
              onClick={() => {
                setHistory((h) => [...h.slice(-(HISTORY_LIMIT - 1)), data]);
                replaceData(initialPreviewData(lead));
              }}
            >
              Przywróć ustawienia startowe
            </button>
          </aside>

          <section className="studio-stage" ref={stageRef}>
            <div className="studio-toolbar">
              <Segmented
                options={LAYOUTS}
                value={layout}
                onChange={setLayout}
                label="Układ obrazka"
                className="segmented-4"
              />
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

function Field({
  label,
  hint,
  hintWarning = false,
  children,
}: {
  label: string;
  hint?: string;
  hintWarning?: boolean;
  children: ReactNode;
}) {
  return (
    <div className="field">
      <span className="field-label">{label}</span>
      {children}
      {hint && <span className={`hint ${hintWarning ? 'warning' : ''}`}>{hint}</span>}
    </div>
  );
}

function Segmented<T extends string>({
  options,
  value,
  onChange,
  label,
  className = '',
}: {
  options: Option<T>[];
  value: T;
  onChange: (value: T) => void;
  label: string;
  className?: string;
}) {
  return (
    <div className={`segmented ${className}`} role="radiogroup" aria-label={label}>
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          role="radio"
          aria-checked={value === o.value}
          className={value === o.value ? 'active' : ''}
          onClick={() => onChange(o.value)}
        >
          {o.label}
        </button>
      ))}
    </div>
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

/** Zapisana makieta uzupełniona o pola dodane w nowszych wersjach kreatora (z ustawień startowych). */
function loadSaved(lead: Lead): PreviewData | null {
  try {
    const saved = localStorage.getItem(storageKey(lead.id));
    if (!saved) return null;
    const initial = initialPreviewData(lead);
    const parsed = JSON.parse(saved) as Partial<PreviewData>;
    return {
      ...initial,
      ...parsed,
      sections: { ...initial.sections, ...parsed.sections },
      photos: parsed.photos?.filter((p) => !isUploadedPhoto(p)) ?? initial.photos,
    };
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
