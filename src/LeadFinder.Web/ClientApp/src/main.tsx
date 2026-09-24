import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

// Service worker działa tylko w bezpiecznym kontekście (HTTPS lub localhost) – na serwerze Azure jest HTTPS.
if ('serviceWorker' in navigator && window.isSecureContext && import.meta.env.PROD) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // Brak service workera nie psuje aplikacji – tracimy tylko ekran "brak połączenia".
    });
  });
}
