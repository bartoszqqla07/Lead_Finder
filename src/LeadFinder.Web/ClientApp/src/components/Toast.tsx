import { useCallback, useRef, useState } from 'react';

export interface ToastMessage {
  id: number;
  text: string;
  kind: 'success' | 'error';
}

export function useToast() {
  const [toast, setToast] = useState<ToastMessage | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const showToast = useCallback((text: string, kind: ToastMessage['kind'] = 'success') => {
    clearTimeout(timer.current);
    setToast({ id: Date.now(), text, kind });
    // Błędy wiszą dłużej – trzeba zdążyć je przeczytać.
    timer.current = setTimeout(() => setToast(null), kind === 'error' ? 8000 : 4000);
  }, []);

  return { toast, showToast };
}

export function Toast({ toast }: { toast: ToastMessage | null }) {
  if (!toast) return null;
  return (
    <div key={toast.id} className={`toast toast-${toast.kind}`} role={toast.kind === 'error' ? 'alert' : 'status'}>
      {toast.text}
    </div>
  );
}
