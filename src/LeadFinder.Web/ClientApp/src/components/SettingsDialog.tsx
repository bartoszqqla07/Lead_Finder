import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../api';
import type { Settings } from '../types';

interface Props {
  settings: Settings;
  onClose: () => void;
  onSaved: (settings: Settings) => void;
}

export function SettingsDialog({ settings, onClose, onSaved }: Props) {
  const [apiKey, setApiKey] = useState('');
  const [senderName, setSenderName] = useState(settings.senderName);
  const [signature, setSignature] = useState(settings.signature);
  const [contactEmail, setContactEmail] = useState(settings.contactEmail);
  const [postalAddress, setPostalAddress] = useState(settings.postalAddress);
  const [freeLimit, setFreeLimit] = useState(String(settings.freeMonthlyRequests));
  const [portfolioUrl, setPortfolioUrl] = useState(settings.portfolioUrl);
  const [offerPrice, setOfferPrice] = useState(settings.offerPrice);
  const [carePlan, setCarePlan] = useState(settings.carePlan);
  const [deliveryTime, setDeliveryTime] = useState(settings.deliveryTime);
  const [status, setStatus] = useState<{ ok: boolean; message: string } | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  const save = async (event: FormEvent) => {
    event.preventDefault();
    setIsBusy(true);
    setStatus(null);
    try {
      const saved = await api.updateSettings({
        apiKey: apiKey.trim() ? apiKey.trim() : null,
        senderName,
        signature,
        contactEmail,
        postalAddress,
        portfolioUrl,
        offerPrice,
        carePlan,
        deliveryTime,
        freeMonthlyRequests: Number.parseInt(freeLimit, 10) || 0,
      });
      onSaved(saved);
      setApiKey('');

      // Nowy klucz od razu sprawdzamy bezpłatnym zapytaniem, żeby błąd wyszedł teraz, a nie przy wyszukiwaniu.
      if (apiKey.trim()) setStatus(await api.verifyApiKey());
      else onClose();
    } catch (e) {
      setStatus({ ok: false, message: (e as Error).message });
    } finally {
      setIsBusy(false);
    }
  };

  const verify = async () => {
    setIsBusy(true);
    try {
      setStatus(await api.verifyApiKey());
    } catch (e) {
      setStatus({ ok: false, message: (e as Error).message });
    } finally {
      setIsBusy(false);
    }
  };

  const removeKey = async () => {
    setIsBusy(true);
    try {
      onSaved(await api.updateSettings({ apiKey: '' }));
      setStatus(null);
    } finally {
      setIsBusy(false);
    }
  };

  const keyStatus =
    settings.apiKeySource === 'Environment'
      ? `Używany klucz ze zmiennej środowiskowej / pliku .env (${settings.apiKeyHint}). Ma pierwszeństwo przed kluczem wpisanym tutaj.`
      : settings.apiKeySource === 'App'
        ? `Zapisany klucz: ${settings.apiKeyHint}`
        : 'Brak klucza.';

  return (
    <>
      <div className="overlay" onClick={onClose} />
      <form className="dialog" role="dialog" aria-modal="true" aria-labelledby="settings-title" onSubmit={save}>
        <header className="drawer-header">
          <h2 id="settings-title">Ustawienia</h2>
          <button type="button" className="icon-button" onClick={onClose} aria-label="Zamknij">
            ×
          </button>
        </header>

        <div className="dialog-body">
          <section className="drawer-section">
            <h3>Klucz Google Places API</h3>
            <p className={`hint ${settings.hasApiKey ? '' : 'warning'}`}>{keyStatus}</p>
            <input
              className="input"
              type="password"
              placeholder={settings.hasApiKey ? 'Wklej nowy klucz, żeby go zmienić' : 'AIza…'}
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              autoComplete="off"
            />
            <div className="button-row">
              {settings.hasApiKey && (
                <button type="button" className="button button-small" onClick={verify} disabled={isBusy}>
                  Sprawdź klucz
                </button>
              )}
              {settings.apiKeySource === 'App' && (
                <button type="button" className="link-button danger" onClick={removeKey} disabled={isBusy}>
                  Usuń zapisany klucz
                </button>
              )}
            </div>
            {status && <p className={status.ok ? 'form-success' : 'form-error'}>{status.message}</p>}
            <p className="hint">
              Klucz utworzysz w Google Cloud Console (APIs &amp; Services → Credentials) po włączeniu „Places API
              (New)”. Sprawdzenie klucza jest bezpłatne. Instrukcja krok po kroku: README.md.
            </p>
          </section>

          <section className="drawer-section">
            <h3>Dane nadawcy w wiadomościach</h3>
            <label className="field">
              <span className="field-label">Imię (w zdaniu „nazywam się …”)</span>
              <input
                className="input"
                value={senderName}
                onChange={(e) => setSenderName(e.target.value)}
                placeholder={settings.defaultSenderName}
              />
            </label>
            <label className="field">
              <span className="field-label">Podpis</span>
              <textarea
                className="input textarea"
                rows={3}
                value={signature}
                onChange={(e) => setSignature(e.target.value)}
                placeholder={settings.defaultSignature}
              />
            </label>
            <label className="field">
              <span className="field-label">E-mail kontaktowy</span>
              <input
                className="input"
                type="email"
                value={contactEmail}
                onChange={(e) => setContactEmail(e.target.value)}
                placeholder="kontakt@twojadomena.pl"
              />
            </label>
            <label className="field">
              <span className="field-label">Adres do korespondencji (nagłówek listu)</span>
              <textarea
                className="input textarea"
                rows={3}
                value={postalAddress}
                onChange={(e) => setPostalAddress(e.target.value)}
                placeholder={'Imię Nazwisko\nul. Przykładowa 1\n00-000 Miasto'}
              />
            </label>
            <p className="hint">
              E-mail i adres trafiają do listów papierowych i klauzuli RODO. Administrator danych musi podać, jak się z
              nim skontaktować.
            </p>
          </section>

          <section className="drawer-section">
            <h3>Oferta (wiadomości po zgodzie klienta)</h3>
            <label className="field">
              <span className="field-label">Link do portfolio</span>
              <input
                className="input"
                value={portfolioUrl}
                onChange={(e) => setPortfolioUrl(e.target.value)}
                placeholder="np. twojastrona.pl/projekty"
              />
            </label>
            <div className="field-grid">
              <label className="field">
                <span className="field-label">Cena wykonania</span>
                <input
                  className="input"
                  value={offerPrice}
                  onChange={(e) => setOfferPrice(e.target.value)}
                  placeholder="np. od 1500 zł"
                />
              </label>
              <label className="field">
                <span className="field-label">Czas realizacji</span>
                <input
                  className="input"
                  value={deliveryTime}
                  onChange={(e) => setDeliveryTime(e.target.value)}
                  placeholder="np. 2–3 tygodnie"
                />
              </label>
            </div>
            <label className="field">
              <span className="field-label">Opieka / hosting (opcjonalnie)</span>
              <input
                className="input"
                value={carePlan}
                onChange={(e) => setCarePlan(e.target.value)}
                placeholder="np. 100–150 zł miesięcznie"
              />
            </label>
            <p className="hint">
              Trafiają do „Oferty – krótkiej”, „Oferty – pełnej”, „Odpowiedzi: cena” i listu. Nigdy do pierwszej
              wiadomości elektronicznej – ta zawiera tylko prośbę o zgodę.
            </p>
          </section>

          <section className="drawer-section">
            <h3>Darmowy limit Google</h3>
            <label className="field">
              <span className="field-label">Darmowych zapytań Text Search miesięcznie</span>
              <input
                className="input"
                type="number"
                min={0}
                step={100}
                value={freeLimit}
                onChange={(e) => setFreeLimit(e.target.value)}
              />
            </label>
            <p className="hint">
              Domyślnie 1000 – tyle według cennika Google z marca 2025 obejmuje SKU Text Search Enterprise, z którego
              korzysta LeadFinder. Licznik liczy zapytania wysłane z tej aplikacji; sprawdź zużycie w Google Cloud
              Console, jeśli używasz klucza gdzie indziej.
            </p>
          </section>

          <p className="hint">
            Dane aplikacji: <code>{settings.dataDirectory}</code>
          </p>
        </div>

        <footer className="drawer-footer">
          <button type="button" className="button button-ghost" onClick={onClose}>
            Zamknij
          </button>
          <button type="submit" className="button button-primary" disabled={isBusy}>
            {isBusy ? 'Zapisuję…' : 'Zapisz'}
          </button>
        </footer>
      </form>
    </>
  );
}
