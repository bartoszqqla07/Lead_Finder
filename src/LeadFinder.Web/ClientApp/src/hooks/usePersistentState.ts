import { useEffect, useState } from 'react';

/**
 * useState zapamiętywany w localStorage – filtry i sortowanie przetrwają zamknięcie aplikacji.
 * Zapisany obiekt jest łączony z wartością domyślną, więc po dodaniu nowego pola (np. nowego filtra)
 * stary zapis nadal działa. Uszkodzony albo niedostępny localStorage = wartość domyślna.
 */
export function usePersistentState<T extends object>(key: string, defaultValue: T) {
  const [value, setValue] = useState<T>(() => {
    try {
      const saved = localStorage.getItem(key);
      return saved ? { ...defaultValue, ...(JSON.parse(saved) as Partial<T>) } : defaultValue;
    } catch {
      return defaultValue;
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem(key, JSON.stringify(value));
    } catch {
      // Brak miejsca albo tryb prywatny – filtry po prostu nie zostaną zapamiętane.
    }
  }, [key, value]);

  return [value, setValue] as const;
}
