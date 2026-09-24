import { useEffect, useState } from 'react';

/** Szerokość, poniżej której UI przechodzi w układ telefonu (karty zamiast tabeli, zwijane panele). */
export const MOBILE_QUERY = '(max-width: 720px)';

/** Czy media query jest spełnione – aktualizuje się przy obracaniu telefonu i zmianie rozmiaru okna. */
export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(() => window.matchMedia(query).matches);

  useEffect(() => {
    const media = window.matchMedia(query);
    const update = () => setMatches(media.matches);
    update();
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, [query]);

  return matches;
}

export const useIsMobile = () => useMediaQuery(MOBILE_QUERY);
