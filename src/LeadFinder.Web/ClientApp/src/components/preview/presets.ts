import { plural } from '../../labels';

/** Styl makiety: ciemny (barber, tatuaż), jasny elegancki (fryzjer, beauty, spa) albo pastelowy (paznokcie). */
export type PreviewStyle = 'dark' | 'light' | 'pastel';

export interface ServiceItem {
  name: string;
  price: string;
}

/** Wszystko, co trafia na makietę – edytowalne w kreatorze. */
export interface PreviewData {
  name: string;
  tagline: string;
  /** Mały napis nad nazwą, np. "★ 4,9 · 312 opinii w Google"; pusty = brak. */
  badge: string;
  style: PreviewStyle;
  accent: string;
  address: string;
  phone: string;
  hours: string;
  services: ServiceItem[];
  /** Adresy obrazków (blob: z wgranych plików); pierwszy to zdjęcie główne. */
  photos: string[];
}

interface Preset {
  style: PreviewStyle;
  accent: string;
  tagline: (city: string) => string;
  services: ServiceItem[];
}

/**
 * Punkty startowe dla branż. Ceny są PRZYKŁADOWE – kreator o tym przypomina, żeby podmienić je
 * na prawdziwe z Booksy/Instagrama salonu albo usunąć.
 */
const PRESETS: Record<string, Preset> = {
  barber: {
    style: 'dark',
    accent: '#c8a165',
    tagline: (city) => `Klasyczne cięcia, broda i dobra atmosfera – ${city}`,
    services: [
      { name: 'Strzyżenie męskie', price: '70 zł' },
      { name: 'Strzyżenie + broda', price: '110 zł' },
      { name: 'Trymowanie brody', price: '50 zł' },
      { name: 'Strzyżenie dziecięce', price: '50 zł' },
    ],
  },
  hair: {
    style: 'light',
    accent: '#b08968',
    tagline: (city) => `Fryzury dopasowane do Ciebie – ${city}`,
    services: [
      { name: 'Strzyżenie damskie', price: 'od 90 zł' },
      { name: 'Koloryzacja', price: 'od 220 zł' },
      { name: 'Modelowanie', price: 'od 70 zł' },
      { name: 'Keratynowe prostowanie', price: 'od 400 zł' },
    ],
  },
  beauty: {
    style: 'light',
    accent: '#c48b8b',
    tagline: () => 'Zabiegi, po których poczujesz się pięknie',
    services: [
      { name: 'Oczyszczanie wodorowe', price: '180 zł' },
      { name: 'Henna i regulacja brwi', price: '60 zł' },
      { name: 'Manicure hybrydowy', price: '110 zł' },
      { name: 'Depilacja woskiem', price: 'od 40 zł' },
    ],
  },
  nails: {
    style: 'pastel',
    accent: '#d9779f',
    tagline: () => 'Stylizacje paznokci, które cieszą oko',
    services: [
      { name: 'Manicure hybrydowy', price: '110 zł' },
      { name: 'Przedłużanie żelem', price: '170 zł' },
      { name: 'Uzupełnienie żelu', price: '140 zł' },
      { name: 'Pedicure hybrydowy', price: '140 zł' },
    ],
  },
  spa: {
    style: 'light',
    accent: '#7d8f6e',
    tagline: () => 'Chwila tylko dla Ciebie',
    services: [
      { name: 'Masaż relaksacyjny 60 min', price: '180 zł' },
      { name: 'Rytuał pielęgnacyjny na twarz', price: '220 zł' },
      { name: 'Masaż gorącymi kamieniami', price: '220 zł' },
      { name: 'Voucher podarunkowy', price: 'od 100 zł' },
    ],
  },
  tattoo: {
    style: 'dark',
    accent: '#d64545',
    tagline: () => 'Autorskie projekty i precyzyjne wykonanie',
    services: [
      { name: 'Konsultacja i projekt', price: 'bezpłatnie' },
      { name: 'Mały tatuaż', price: 'od 300 zł' },
      { name: 'Sesja (godzina)', price: '250 zł' },
      { name: 'Cover-up', price: 'wycena' },
    ],
  },
  cosmetology: {
    style: 'light',
    accent: '#5f93a8',
    tagline: (city) => `Profesjonalna kosmetologia – ${city}`,
    services: [
      { name: 'Konsultacja kosmetologiczna', price: '100 zł' },
      { name: 'Peeling medyczny', price: 'od 250 zł' },
      { name: 'Mezoterapia mikroigłowa', price: 'od 350 zł' },
      { name: 'Oczyszczanie wodorowe', price: '200 zł' },
    ],
  },
};

const DEFAULT_PRESET: Preset = {
  style: 'light',
  accent: '#4f46e5',
  tagline: (city) => `Profesjonalne usługi – ${city}`,
  services: [
    { name: 'Usługa 1', price: '100 zł' },
    { name: 'Usługa 2', price: '150 zł' },
    { name: 'Usługa 3', price: '200 zł' },
  ],
};

/** Startowe dane makiety dla leada – z jego nazwy, adresu, telefonu i branży. */
export function initialPreviewData(lead: {
  name: string;
  city: string;
  address: string | null;
  phone: string | null;
  categoryTone: string;
  rating: number | null;
  userRatingCount: number | null;
}): PreviewData {
  const preset = PRESETS[lead.categoryTone] ?? DEFAULT_PRESET;
  // Dobre opinie z Google to najlepszy "haczyk" na makiecie – pokazujemy je, gdy są czym się chwalić.
  const badge =
    lead.rating !== null && lead.rating >= 4.5 && (lead.userRatingCount ?? 0) >= 10
      ? `★ ${lead.rating.toFixed(1).replace('.', ',')} · ${lead.userRatingCount} ${plural(lead.userRatingCount ?? 0, 'opinia', 'opinie', 'opinii')} w Google`
      : '';
  return {
    name: lead.name,
    tagline: preset.tagline(lead.city),
    badge,
    style: preset.style,
    accent: preset.accent,
    address: lead.address ?? lead.city,
    phone: lead.phone ?? '',
    hours: 'Pon–Pt 9:00–19:00 · Sob 9:00–15:00',
    services: preset.services,
    photos: [],
  };
}

/** Cennik jako tekst do edycji: "Usługa | cena" w każdej linii. */
export const servicesToText = (services: ServiceItem[]) => services.map((s) => `${s.name} | ${s.price}`).join('\n');

export const textToServices = (text: string): ServiceItem[] =>
  text
    .split('\n')
    .map((line) => line.split('|'))
    .filter(([name]) => name && name.trim())
    .map(([name, price]) => ({ name: name!.trim(), price: (price ?? '').trim() }));
