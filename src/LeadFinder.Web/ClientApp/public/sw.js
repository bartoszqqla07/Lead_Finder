// Service worker LeadFinder – celowo minimalny.
// Dane (/api) i zasoby aplikacji zawsze idą z sieci: leady muszą być aktualne, a pliki JS/CSS
// mają w nazwach hash, więc przeglądarka i tak cache'uje je poprawnie.
// Jedyne zadanie: gdy nie ma internetu, zamiast błędu przeglądarki pokazać /offline.html.

const CACHE = 'leadfinder-shell-v1';
const OFFLINE_URL = '/offline.html';

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll([OFFLINE_URL, '/icons/icon-192.png'])));
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((key) => key !== CACHE).map((key) => caches.delete(key)))),
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  // Tylko nawigacja (otwarcie strony); wszystko inne przechodzi bez zmian.
  if (event.request.mode !== 'navigate') return;
  event.respondWith(fetch(event.request).catch(() => caches.match(OFFLINE_URL)));
});
