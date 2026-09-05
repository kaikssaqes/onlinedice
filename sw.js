const CACHE = 'online-dice-v1';
const ASSETS = [
  './',
  './index.html',
  './manifest.json',
  './img/bg-lg.png',
  './img/light.png',
  './img/logo-new.png',
  './img/icon-192.png',
  './img/icon-512.png',
  './img/favicon.ico',
  './fonts/Marvin-webfont.woff2',
  './fonts/dicefont.woff',
  './sounds/roll.mp3',
  './sounds/success.mp3'
];

self.addEventListener('install', (e) => {
  e.waitUntil(
    caches.open(CACHE).then((c) => c.addAll(ASSETS)).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (e) => {
  e.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (e) => {
  if (e.request.method !== 'GET') return;
  e.respondWith(
    caches.match(e.request).then((cached) => {
      const fetchPromise = fetch(e.request).then((res) => {
        if (res && res.status === 200 && res.type === 'basic') {
          const clone = res.clone();
          caches.open(CACHE).then((c) => c.put(e.request, clone));
        }
        return res;
      }).catch(() => cached);
      return cached || fetchPromise;
    })
  );
});
