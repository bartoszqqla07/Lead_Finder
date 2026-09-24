import { plural } from '../../labels';

/** Styl makiety: ciemny (barber, tatuaż), jasny elegancki (fryzjer, beauty, spa) albo pastelowy (paznokcie). */
export type PreviewStyle = 'dark' | 'light' | 'pastel';

/**
 * Układ nagłówka strony: zdjęcie na całą szerokość z tekstem po lewej, tekst obok zdjęcia
 * albo wszystko wyśrodkowane – trzy różne "charaktery" tej samej treści.
 */
export type HeroLayout = 'overlay' | 'split' | 'centered';

export type IconName = 'clock' | 'scissors' | 'pin' | 'star' | 'sparkle' | 'leaf' | 'heart' | 'shield' | 'drop' | 'pen';

export interface ServiceItem {
  name: string;
  price: string;
}

export interface FeatureItem {
  title: string;
  text: string;
}

/** Wszystko, co trafia na makietę – edytowalne w kreatorze. */
export interface PreviewData {
  name: string;
  /** Mały napis nad nazwą, np. "Barbershop · Katowice". */
  eyebrow: string;
  tagline: string;
  /** Ocena z Google, np. "4,9"; pusta = bez karty z opiniami. */
  rating: string;
  /** Liczba opinii jako tekst, np. "312". */
  reviews: string;
  style: PreviewStyle;
  hero: HeroLayout;
  accent: string;
  address: string;
  phone: string;
  hours: string;
  services: ServiceItem[];
  /** Trzy atuty salonu pod nagłówkiem strony. */
  features: FeatureItem[];
  /** Ikony atutów – z branży, w kolejności atutów. */
  icons: IconName[];
  /** Hasło w pasku rezerwacji na dole strony. */
  ctaTitle: string;
  /** Adresy obrazków (blob: z wgranych plików); pierwszy to zdjęcie główne. */
  photos: string[];
}

interface Preset {
  style: PreviewStyle;
  hero: HeroLayout;
  accent: string;
  eyebrow: string;
  tagline: string;
  services: ServiceItem[];
  features: (city: string) => FeatureItem[];
  icons: IconName[];
  ctaTitle: string;
}

/**
 * Punkty startowe dla branż. Ceny są PRZYKŁADOWE – kreator o tym przypomina, żeby podmienić je
 * na prawdziwe z Booksy/Instagrama salonu albo usunąć.
 */
const PRESETS: Record<string, Preset> = {
  barber: {
    style: 'dark',
    hero: 'overlay',
    accent: '#c8a165',
    eyebrow: 'Barbershop',
    tagline: 'Klasyczne cięcia, dopracowana broda i dobra atmosfera.',
    services: [
      { name: 'Strzyżenie męskie', price: '70 zł' },
      { name: 'Strzyżenie + broda', price: '110 zł' },
      { name: 'Trymowanie brody', price: '50 zł' },
      { name: 'Strzyżenie dziecięce', price: '50 zł' },
    ],
    features: (city) => [
      { title: 'Rezerwacja 24/7', text: 'Wolny termin sprawdzisz w kilka sekund' },
      { title: 'Doświadczeni barberzy', text: 'Klasyka, fade i konturowanie brody' },
      { title: city, text: 'Łatwy dojazd i dobra atmosfera' },
    ],
    icons: ['clock', 'scissors', 'pin'],
    ctaTitle: 'Wpadnij na strzyżenie – termin zarezerwujesz online',
  },
  hair: {
    style: 'light',
    hero: 'split',
    accent: '#b08968',
    eyebrow: 'Salon fryzjerski',
    tagline: 'Fryzury dopasowane do Ciebie – od cięcia po koloryzację.',
    services: [
      { name: 'Strzyżenie damskie', price: 'od 90 zł' },
      { name: 'Koloryzacja', price: 'od 220 zł' },
      { name: 'Modelowanie', price: 'od 70 zł' },
      { name: 'Keratynowe prostowanie', price: 'od 400 zł' },
    ],
    features: (city) => [
      { title: 'Indywidualna konsultacja', text: 'Dobierzemy fryzurę i kolor do Ciebie' },
      { title: 'Profesjonalne kosmetyki', text: 'Pielęgnacja, którą widać po wizycie' },
      { title: city, text: 'Rezerwacja online w kilka sekund' },
    ],
    icons: ['scissors', 'drop', 'pin'],
    ctaTitle: 'Umów się na wizytę – wybierz termin online',
  },
  beauty: {
    style: 'light',
    hero: 'split',
    accent: '#c48b8b',
    eyebrow: 'Salon kosmetyczny',
    tagline: 'Zabiegi, po których poczujesz się pięknie.',
    services: [
      { name: 'Oczyszczanie wodorowe', price: '180 zł' },
      { name: 'Henna i regulacja brwi', price: '60 zł' },
      { name: 'Manicure hybrydowy', price: '110 zł' },
      { name: 'Depilacja woskiem', price: 'od 40 zł' },
    ],
    features: (city) => [
      { title: 'Sprawdzone zabiegi', text: 'Efekty widoczne od pierwszej wizyty' },
      { title: 'Chwila dla siebie', text: 'Spokojna, zadbana przestrzeń' },
      { title: city, text: 'Rezerwacja online w kilka sekund' },
    ],
    icons: ['sparkle', 'heart', 'pin'],
    ctaTitle: 'Zarezerwuj zabieg – wybierz dogodny termin',
  },
  nails: {
    style: 'pastel',
    hero: 'centered',
    accent: '#d9779f',
    eyebrow: 'Stylizacja paznokci',
    tagline: 'Stylizacje, które cieszą oko przez długie tygodnie.',
    services: [
      { name: 'Manicure hybrydowy', price: '110 zł' },
      { name: 'Przedłużanie żelem', price: '170 zł' },
      { name: 'Uzupełnienie żelu', price: '140 zł' },
      { name: 'Pedicure hybrydowy', price: '140 zł' },
    ],
    features: (city) => [
      { title: 'Trwałe stylizacje', text: 'Starannie, precyzyjnie, bez odprysków' },
      { title: 'Sterylne narzędzia', text: 'Bezpieczeństwo przy każdej wizycie' },
      { title: city, text: 'Rezerwacja online w kilka sekund' },
    ],
    icons: ['sparkle', 'shield', 'pin'],
    ctaTitle: 'Czas na nowe paznokcie? Zarezerwuj termin',
  },
  spa: {
    style: 'light',
    hero: 'centered',
    accent: '#7d8f6e',
    eyebrow: 'Spa & masaż',
    tagline: 'Chwila tylko dla Ciebie – odpocznij i zregeneruj się.',
    services: [
      { name: 'Masaż relaksacyjny 60 min', price: '180 zł' },
      { name: 'Rytuał pielęgnacyjny na twarz', price: '220 zł' },
      { name: 'Masaż gorącymi kamieniami', price: '220 zł' },
      { name: 'Voucher podarunkowy', price: 'od 100 zł' },
    ],
    features: (city) => [
      { title: 'Pełen relaks', text: 'Spokój, cisza i doświadczone dłonie' },
      { title: 'Naturalna pielęgnacja', text: 'Starannie dobrane kosmetyki' },
      { title: city, text: 'Vouchery i rezerwacja online' },
    ],
    icons: ['leaf', 'drop', 'pin'],
    ctaTitle: 'Podaruj sobie chwilę relaksu – zarezerwuj online',
  },
  tattoo: {
    style: 'dark',
    hero: 'overlay',
    accent: '#d64545',
    eyebrow: 'Studio tatuażu',
    tagline: 'Autorskie projekty i precyzyjne wykonanie.',
    services: [
      { name: 'Konsultacja i projekt', price: 'bezpłatnie' },
      { name: 'Mały tatuaż', price: 'od 300 zł' },
      { name: 'Sesja (godzina)', price: '250 zł' },
      { name: 'Cover-up', price: 'wycena' },
    ],
    features: (city) => [
      { title: 'Autorskie projekty', text: 'Każdy wzór przygotowany od zera' },
      { title: 'Sterylne warunki', text: 'Jednorazowe igły, certyfikowane tusze' },
      { title: city, text: 'Konsultacje po wcześniejszym umówieniu' },
    ],
    icons: ['pen', 'shield', 'pin'],
    ctaTitle: 'Masz pomysł na tatuaż? Umów konsultację',
  },
  cosmetology: {
    style: 'light',
    hero: 'split',
    accent: '#5f93a8',
    eyebrow: 'Gabinet kosmetologii',
    tagline: 'Profesjonalna pielęgnacja oparta na wiedzy.',
    services: [
      { name: 'Konsultacja kosmetologiczna', price: '100 zł' },
      { name: 'Peeling medyczny', price: 'od 250 zł' },
      { name: 'Mezoterapia mikroigłowa', price: 'od 350 zł' },
      { name: 'Oczyszczanie wodorowe', price: '200 zł' },
    ],
    features: (city) => [
      { title: 'Indywidualny plan', text: 'Zabiegi dobrane do potrzeb skóry' },
      { title: 'Nowoczesny sprzęt', text: 'Bezpieczne, skuteczne metody' },
      { title: city, text: 'Rezerwacja online w kilka sekund' },
    ],
    icons: ['sparkle', 'shield', 'pin'],
    ctaTitle: 'Zadbaj o swoją skórę – umów konsultację',
  },
};

const DEFAULT_PRESET: Preset = {
  style: 'light',
  hero: 'split',
  accent: '#4f46e5',
  eyebrow: 'Salon',
  tagline: 'Profesjonalne usługi w przyjaznej atmosferze.',
  services: [
    { name: 'Usługa 1', price: '100 zł' },
    { name: 'Usługa 2', price: '150 zł' },
    { name: 'Usługa 3', price: '200 zł' },
  ],
  features: (city) => [
    { title: 'Doświadczenie', text: 'Profesjonalna obsługa' },
    { title: 'Jakość', text: 'Sprawdzone produkty i metody' },
    { title: city, text: 'Rezerwacja online w kilka sekund' },
  ],
  icons: ['star', 'shield', 'pin'],
  ctaTitle: 'Zarezerwuj wizytę online',
};

/** Startowe dane makiety dla leada – z jego nazwy, adresu, telefonu, oceny i branży. */
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
  // Dobre opinie z Google to najlepszy "haczyk" na makiecie – pokazujemy je, gdy jest czym się chwalić.
  const goodReviews = lead.rating !== null && lead.rating >= 4.5 && (lead.userRatingCount ?? 0) >= 10;
  return {
    name: lead.name,
    eyebrow: `${preset.eyebrow} · ${lead.city}`,
    tagline: preset.tagline,
    rating: goodReviews ? lead.rating!.toFixed(1).replace('.', ',') : '',
    reviews: goodReviews ? String(lead.userRatingCount) : '',
    style: preset.style,
    hero: preset.hero,
    accent: preset.accent,
    address: lead.address ?? lead.city,
    phone: lead.phone ?? '',
    hours: 'Pon–Pt 9:00–19:00 · Sob 9:00–15:00',
    services: preset.services,
    features: preset.features(lead.city),
    icons: preset.icons,
    ctaTitle: preset.ctaTitle,
    photos: [],
  };
}

/** "312 opinii w Google" – z poprawną odmianą. */
export function reviewsLabel(reviews: string): string {
  const count = Number.parseInt(reviews.replace(/\s/g, ''), 10);
  return Number.isFinite(count) ? `${reviews} ${plural(count, 'opinia', 'opinie', 'opinii')} w Google` : 'opinie w Google';
}

/** Lista jako tekst do edycji: "lewa | prawa" w każdej linii (cennik: usługa | cena, atuty: tytuł | opis). */
const pairsToText = (pairs: [string, string][]) => pairs.map(([a, b]) => `${a} | ${b}`).join('\n');

const textToPairs = (text: string): [string, string][] =>
  text
    .split('\n')
    .map((line) => line.split('|'))
    .filter(([a]) => a && a.trim())
    .map(([a, b]) => [a!.trim(), (b ?? '').trim()]);

export const servicesToText = (services: ServiceItem[]) => pairsToText(services.map((s) => [s.name, s.price]));
export const textToServices = (text: string): ServiceItem[] => textToPairs(text).map(([name, price]) => ({ name, price }));
export const featuresToText = (features: FeatureItem[]) => pairsToText(features.map((f) => [f.title, f.text]));
export const textToFeatures = (text: string): FeatureItem[] =>
  textToPairs(text)
    .slice(0, 3)
    .map(([title, text]) => ({ title, text }));
