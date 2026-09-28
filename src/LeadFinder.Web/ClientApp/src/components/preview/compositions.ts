import {
  isUploadedPhoto,
  stockPhotos,
  type ButtonShape,
  type FeatureItem,
  type FontPair,
  type HeroLayout,
  type IconName,
  type PreviewData,
  type PreviewStyle,
} from './presets';

/**
 * Losowe kompozycje ukierunkowane na branżę: każda branża ma własne pule stylów, kolorów, haseł i atutów,
 * więc "losuj" daje różne, ale zawsze pasujące do salonu warianty. Nie zmienia danych salonu
 * (nazwa, kontakt, ocena, cennik) ani zdjęć wgranych przez użytkownika.
 */
interface TonePool {
  /** Style z powtórzeniami = wagi (częściej pierwszy). */
  styles: PreviewStyle[];
  accents: string[];
  eyebrows: string[];
  taglines: string[];
  ctaTitles: string[];
  /** Atuty do wylosowania; trzeci atut to zawsze lokalizacja (miasto). */
  features: (FeatureItem & { icon: IconName })[];
  informal: boolean;
}

const POOLS: Record<string, TonePool> = {
  barber: {
    styles: ['dark', 'dark', 'dark', 'light'],
    accents: ['#c8a165', '#b87333', '#d4af37', '#9aa5b1', '#a33b2b', '#6b8e7a'],
    eyebrows: ['Barbershop', 'Barber & Grooming', 'Męski fryzjer'],
    taglines: [
      'Klasyczne cięcia, dopracowana broda i dobra atmosfera.',
      'Fade, broda i konturowanie – z dbałością o każdy detal.',
      'Miejsce, gdzie strzyżenie to rytuał, a nie pośpiech.',
      'Twój barber od precyzyjnych cięć i dobrze utrzymanej brody.',
    ],
    ctaTitles: [
      'Wpadnij na strzyżenie – termin zarezerwujesz online',
      'Wolny termin sprawdzisz w kilka sekund',
      'Czas na świeże cięcie? Zarezerwuj online',
    ],
    features: [
      { title: 'Rezerwacja 24/7', text: 'Wolny termin sprawdzisz w kilka sekund', icon: 'clock' },
      { title: 'Doświadczeni barberzy', text: 'Klasyka, fade i konturowanie brody', icon: 'scissors' },
      { title: 'Gorący ręcznik', text: 'Golenie brzytwą w klasycznym stylu', icon: 'drop' },
      { title: 'Profesjonalne kosmetyki', text: 'Stylizacja, która trzyma cały dzień', icon: 'sparkle' },
      { title: 'Bez czekania', text: 'Przychodzisz na godzinę, wychodzisz zadowolony', icon: 'star' },
    ],
    informal: true,
  },
  hair: {
    styles: ['light', 'light', 'dark', 'pastel'],
    accents: ['#b08968', '#c9a27e', '#8c6e5d', '#a67c94', '#6f8f86', '#c47f6b'],
    eyebrows: ['Salon fryzjerski', 'Hair Studio', 'Fryzjer'],
    taglines: [
      'Fryzury dopasowane do Ciebie – od cięcia po koloryzację.',
      'Koloryzacja, cięcie i pielęgnacja w jednym miejscu.',
      'Włosy, które pokochasz każdego dnia.',
      'Nowoczesne cięcia i kolory, które podkreślą Twój styl.',
    ],
    ctaTitles: [
      'Umów się na wizytę – wybierz termin online',
      'Czas na zmianę? Zarezerwuj termin online',
      'Twoja nowa fryzura czeka – zarezerwuj wizytę',
    ],
    features: [
      { title: 'Indywidualna konsultacja', text: 'Dobierzemy fryzurę i kolor do Ciebie', icon: 'scissors' },
      { title: 'Profesjonalne kosmetyki', text: 'Pielęgnacja, którą widać po wizycie', icon: 'drop' },
      { title: 'Modna koloryzacja', text: 'Balayage, sombre i odświeżenie koloru', icon: 'sparkle' },
      { title: 'Zdrowe włosy', text: 'Zabiegi regenerujące i keratyna', icon: 'leaf' },
      { title: 'Stali klienci', text: 'Wracają do nas od lat', icon: 'heart' },
    ],
    informal: false,
  },
  beauty: {
    styles: ['light', 'light', 'pastel', 'dark'],
    accents: ['#c48b8b', '#b8860b', '#a86f7f', '#d4a5a5', '#8e7cc3', '#9c6b4e'],
    eyebrows: ['Salon kosmetyczny', 'Beauty Studio', 'Salon urody'],
    taglines: [
      'Zabiegi, po których poczujesz się pięknie.',
      'Brwi, rzęsy i pielęgnacja – wszystko w jednym miejscu.',
      'Naturalne piękno podkreślone z wyczuciem.',
      'Chwila dla siebie w rękach doświadczonych specjalistek.',
    ],
    ctaTitles: [
      'Zarezerwuj zabieg – wybierz dogodny termin',
      'Zadbaj o siebie – umów wizytę online',
      'Wolne terminy sprawdzisz w kilka sekund',
    ],
    features: [
      { title: 'Sprawdzone zabiegi', text: 'Efekty widoczne od pierwszej wizyty', icon: 'sparkle' },
      { title: 'Chwila dla siebie', text: 'Spokojna, zadbana przestrzeń', icon: 'heart' },
      { title: 'Brwi i rzęsy', text: 'Stylizacja dopasowana do rysów twarzy', icon: 'star' },
      { title: 'Higiena przede wszystkim', text: 'Sterylne narzędzia i jednorazowe materiały', icon: 'shield' },
      { title: 'Naturalne kosmetyki', text: 'Delikatne dla skóry', icon: 'leaf' },
    ],
    informal: false,
  },
  nails: {
    styles: ['pastel', 'pastel', 'light', 'dark'],
    accents: ['#d9779f', '#e08e79', '#b77fbd', '#c9a0dc', '#e5989b', '#d4a373'],
    eyebrows: ['Stylizacja paznokci', 'Nail Studio', 'Manicure & Pedicure'],
    taglines: [
      'Stylizacje, które cieszą oko przez długie tygodnie.',
      'Paznokcie dopracowane w każdym detalu.',
      'Manicure, pedicure i zdobienia – na każdą okazję.',
      'Trwałe, piękne i zawsze na czas.',
    ],
    ctaTitles: [
      'Czas na nowe paznokcie? Zarezerwuj termin',
      'Umów się na manicure – online w kilka sekund',
      'Wybierz termin i kolor – resztą zajmiemy się my',
    ],
    features: [
      { title: 'Trwałe stylizacje', text: 'Starannie, precyzyjnie, bez odprysków', icon: 'sparkle' },
      { title: 'Sterylne narzędzia', text: 'Bezpieczeństwo przy każdej wizycie', icon: 'shield' },
      { title: 'Setki kolorów', text: 'Od klasyki po modne zdobienia', icon: 'drop' },
      { title: 'Zdrowa płytka', text: 'Delikatne przygotowanie paznokcia', icon: 'leaf' },
      { title: 'Stałe klientki', text: 'Wracają do nas co kilka tygodni', icon: 'heart' },
    ],
    informal: true,
  },
  spa: {
    styles: ['light', 'light', 'dark'],
    accents: ['#7d8f6e', '#a68a64', '#6b7f8e', '#b5838d', '#8a9a5b', '#c2a878'],
    eyebrows: ['Spa & masaż', 'Day Spa', 'Strefa relaksu'],
    taglines: [
      'Chwila tylko dla Ciebie – odpocznij i zregeneruj się.',
      'Masaże i rytuały, które przywracają spokój.',
      'Odetchnij od codzienności w naszym spa.',
      'Relaks dla ciała i głowy w jednym miejscu.',
    ],
    ctaTitles: [
      'Podaruj sobie chwilę relaksu – zarezerwuj online',
      'Szukasz prezentu? Mamy vouchery podarunkowe',
      'Zarezerwuj masaż w dogodnym terminie',
    ],
    features: [
      { title: 'Pełen relaks', text: 'Spokój, cisza i doświadczone dłonie', icon: 'leaf' },
      { title: 'Naturalna pielęgnacja', text: 'Starannie dobrane kosmetyki', icon: 'drop' },
      { title: 'Vouchery', text: 'Idealny prezent dla bliskiej osoby', icon: 'heart' },
      { title: 'Rytuały dla dwojga', text: 'Wspólny czas we dwoje', icon: 'sparkle' },
      { title: 'Certyfikowani terapeuci', text: 'Masaże dopasowane do potrzeb', icon: 'shield' },
    ],
    informal: false,
  },
  tattoo: {
    styles: ['dark'],
    accents: ['#d64545', '#c8a165', '#8fa3b0', '#e07a2f', '#9b59b6', '#4f9d69'],
    eyebrows: ['Studio tatuażu', 'Tattoo Studio', 'Tattoo & Art'],
    taglines: [
      'Autorskie projekty i precyzyjne wykonanie.',
      'Tatuaże, które opowiadają Twoją historię.',
      'Od pomysłu do projektu – razem stworzymy coś wyjątkowego.',
      'Czysta kreska, sterylne warunki, zero kompromisów.',
    ],
    ctaTitles: [
      'Masz pomysł na tatuaż? Umów konsultację',
      'Zacznijmy od projektu – umów konsultację',
      'Zarezerwuj sesję w dogodnym terminie',
    ],
    features: [
      { title: 'Autorskie projekty', text: 'Każdy wzór przygotowany od zera', icon: 'pen' },
      { title: 'Sterylne warunki', text: 'Jednorazowe igły, certyfikowane tusze', icon: 'shield' },
      { title: 'Cover-upy', text: 'Odświeżymy albo przykryjemy stary tatuaż', icon: 'sparkle' },
      { title: 'Bezpłatna konsultacja', text: 'Omówimy pomysł i wycenę', icon: 'star' },
      { title: 'Pielęgnacja', text: 'Instrukcja i wsparcie po zabiegu', icon: 'drop' },
    ],
    informal: true,
  },
  cosmetology: {
    styles: ['light', 'light', 'pastel'],
    accents: ['#5f93a8', '#7fa7a0', '#b08968', '#8e9aaf', '#c48b8b', '#6d8a96'],
    eyebrows: ['Gabinet kosmetologii', 'Kosmetologia estetyczna', 'Clinic & Skin Care'],
    taglines: [
      'Profesjonalna pielęgnacja oparta na wiedzy.',
      'Zdrowa, promienna skóra – zaplanowana krok po kroku.',
      'Nowoczesne zabiegi i indywidualne podejście do skóry.',
      'Skuteczna kosmetologia z efektem, który widać.',
    ],
    ctaTitles: [
      'Zadbaj o swoją skórę – umów konsultację',
      'Zacznij od konsultacji – dobierzemy zabiegi',
      'Wolne terminy sprawdzisz online',
    ],
    features: [
      { title: 'Indywidualny plan', text: 'Zabiegi dobrane do potrzeb skóry', icon: 'sparkle' },
      { title: 'Nowoczesny sprzęt', text: 'Bezpieczne, skuteczne metody', icon: 'shield' },
      { title: 'Konsultacja', text: 'Analiza skóry przed pierwszym zabiegiem', icon: 'star' },
      { title: 'Profesjonalne kosmetyki', text: 'Pielęgnacja także w domu', icon: 'drop' },
      { title: 'Widoczne efekty', text: 'Seria zabiegów z planem', icon: 'heart' },
    ],
    informal: false,
  },
};

const FONTS_FOR_STYLE: Record<PreviewStyle, FontPair[]> = {
  dark: ['bold', 'bold', 'clean', 'elegant'],
  light: ['elegant', 'elegant', 'clean', 'soft'],
  pastel: ['soft', 'soft', 'elegant', 'clean'],
};

const BUTTONS_FOR_FONT: Record<FontPair, ButtonShape[]> = {
  bold: ['sharp', 'sharp', 'rounded'],
  elegant: ['sharp', 'pill', 'rounded'],
  soft: ['pill', 'pill', 'rounded'],
  clean: ['rounded', 'pill', 'sharp'],
};

const HEROES: HeroLayout[] = ['overlay', 'split', 'centered'];

const CTA_LABELS = {
  formal: ['Zarezerwuj wizytę', 'Umów wizytę', 'Zarezerwuj termin', 'Umów się online'],
  informal: ['Zarezerwuj wizytę', 'Umów się', 'Zarezerwuj termin', 'Wybierz termin'],
};

const pick = <T>(items: readonly T[]): T => items[Math.floor(Math.random() * items.length)]!;

function shuffle<T>(items: readonly T[]): T[] {
  const copy = [...items];
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [copy[i], copy[j]] = [copy[j]!, copy[i]!];
  }
  return copy;
}

/** Klucz wyglądu – żeby kolejne losowanie nie dało prawie tego samego. */
const lookKey = (d: Pick<PreviewData, 'style' | 'font' | 'hero' | 'accent'>) =>
  `${d.style}|${d.font}|${d.hero}|${d.accent}`;

/**
 * Nowa kompozycja dla salonu danej branży. Zachowuje dane salonu, cennik, widoczne sekcje
 * oraz zdjęcia wgrane przez użytkownika (gotowe zdjęcia branżowe są losowane na nowo).
 */
export function randomComposition(current: PreviewData, tone: string, city: string): PreviewData {
  const pool = POOLS[tone] ?? POOLS.beauty!;

  for (let attempt = 0; attempt < 8; attempt++) {
    const style = pick(pool.styles);
    const font = pick(FONTS_FOR_STYLE[style]);
    const hero = pick(HEROES);
    const accent = pick(pool.accents);
    const differsEnough =
      [style !== current.style, font !== current.font, hero !== current.hero, accent !== current.accent].filter(Boolean)
        .length >= 2;
    if (!differsEnough && attempt < 7) continue;
    if (lookKey({ style, font, hero, accent }) === lookKey(current)) continue;

    const [first, second] = shuffle(pool.features);
    const features = [first!, second!, { title: city, text: 'Rezerwacja online w kilka sekund', icon: 'pin' as const }];
    const uploaded = current.photos.filter(isUploadedPhoto);

    return {
      ...current,
      style,
      font,
      hero,
      accent,
      buttons: pick(BUTTONS_FOR_FONT[font]),
      overlay: Math.round((0.72 + Math.random() * 0.16) * 100) / 100,
      eyebrow: `${pick(pool.eyebrows)} · ${city}`,
      tagline: pick(pool.taglines),
      ctaTitle: pick(pool.ctaTitles),
      ctaLabel: pick(pool.informal ? CTA_LABELS.informal : CTA_LABELS.formal),
      features: features.map(({ title, text }) => ({ title, text })),
      icons: features.map((f) => f.icon),
      // Zdjęcia salonu zostają; jeśli ich nie ma – nowy układ gotowych zdjęć branżowych.
      photos: uploaded.length > 0 ? current.photos : shuffle(stockPhotos(tone)).slice(0, 4),
    };
  }
  return current;
}
