import { useState, type FormEvent } from 'react';
import { ApiError, api } from '../api';

interface Props {
  onLoggedIn: () => void;
}

/** Ekran logowania – widoczny tylko, gdy serwer ma ustawione hasło (wersja w internecie). */
export function LoginScreen({ onLoggedIn }: Props) {
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setIsBusy(true);
    setError(null);
    try {
      await api.login(password);
      onLoggedIn();
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 429
          ? 'Za dużo prób. Odczekaj minutę i spróbuj ponownie.'
          : (e as Error).message,
      );
      setPassword('');
    } finally {
      setIsBusy(false);
    }
  };

  return (
    <div className="login">
      <form className="card login-card" onSubmit={submit}>
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            <svg viewBox="0 0 24 24" width="18" height="18">
              <circle cx="10.5" cy="10.5" r="6" fill="none" stroke="currentColor" strokeWidth="2.4" />
              <path d="M15 15l5 5" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
            </svg>
          </span>
          <span className="brand-name">LeadFinder</span>
        </div>

        <label className="field">
          <span className="field-label">Hasło</span>
          <input
            className="input"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            autoFocus
          />
        </label>

        {error && <p className="form-error">{error}</p>}

        <button className="button button-primary button-block" type="submit" disabled={isBusy || !password}>
          {isBusy ? 'Loguję…' : 'Zaloguj'}
        </button>
      </form>
    </div>
  );
}
