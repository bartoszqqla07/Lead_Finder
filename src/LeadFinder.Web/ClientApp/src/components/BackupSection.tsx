import { useState } from 'react';
import { api } from '../api';
import { plural } from '../labels';

/**
 * Przenoszenie danych na inny komputer: pobranie kopii całej bazy i wczytanie jej z powrotem.
 * Kod aplikacji idzie przez GitHuba, a dane (klucz API, dane firm) – tym plikiem, poza gitem.
 */
export function BackupSection() {
  const [status, setStatus] = useState<{ ok: boolean; message: string } | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  const restore = async (file: File | undefined) => {
    if (!file) return;
    const confirmed = window.confirm(
      `Zastąpić obecne dane zawartością pliku „${file.name}”?\n\n` +
        'Obecne dane zostaną zapisane obok jako kopia bezpieczeństwa, więc da się to cofnąć.',
    );
    if (!confirmed) return;

    setIsBusy(true);
    setStatus(null);
    try {
      const result = await api.restoreBackup(file);
      setStatus({
        ok: true,
        message: `Wczytano ${result.leadCount} ${plural(result.leadCount, 'lead', 'leady', 'leadów')}. Odświeżam aplikację…`,
      });
      setTimeout(() => window.location.reload(), 1500);
    } catch (e) {
      setStatus({ ok: false, message: (e as Error).message });
      setIsBusy(false);
    }
  };

  return (
    <section className="drawer-section">
      <h3>Przenoszenie na inny komputer</h3>
      <p className="hint">
        Pobierz kopię tutaj i wczytaj ją w LeadFinderze na drugim komputerze. W jednym pliku są leady, notatki,
        etapy, historia wyszukiwań i ustawienia razem z kluczem API.
      </p>
      <div className="button-row">
        <a className="button button-small" href={api.backupUrl} download>
          ⬇ Pobierz kopię danych
        </a>
        <label className={`button button-small ${isBusy ? 'disabled' : ''}`}>
          <input
            type="file"
            accept=".db"
            hidden
            disabled={isBusy}
            onChange={(e) => {
              void restore(e.target.files?.[0]);
              e.target.value = '';
            }}
          />
          {isBusy ? 'Wczytuję…' : '⬆ Wczytaj kopię…'}
        </label>
      </div>
      {status && <p className={`hint ${status.ok ? 'ok-text' : 'error-text'}`}>{status.message}</p>}
      <p className="hint warning">
        Plik zawiera klucz API i dane firm – nie wrzucaj go na GitHuba ani do publicznych folderów. Przenieś go
        pendrivem, mailem do siebie albo przez prywatny dysk.
      </p>
    </section>
  );
}
