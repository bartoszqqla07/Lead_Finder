import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

// Wcześniejsza wersja (przygotowana pod hosting) rejestrowała service workera. Lokalnie nie jest potrzebny,
// więc usuwamy go, jeśli został w przeglądarce.
navigator.serviceWorker
  ?.getRegistrations()
  .then((registrations) => registrations.forEach((registration) => void registration.unregister()))
  .catch(() => undefined);
