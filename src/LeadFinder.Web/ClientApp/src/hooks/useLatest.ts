import { useEffect, useRef } from 'react';

/**
 * Ref z zawsze aktualną wartością. Pozwala wywołać najnowszy callback z efektu, bez dopisywania go
 * do zależności – inaczej każdy render rodzica (np. przy zdarzeniach SSE) restartowałby efekt.
 */
export function useLatest<T>(value: T) {
  const ref = useRef(value);
  useEffect(() => {
    ref.current = value;
  });
  return ref;
}
