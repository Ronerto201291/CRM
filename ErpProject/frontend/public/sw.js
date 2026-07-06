self.addEventListener('push', (event) => {
    let data = { title: 'ERP', body: 'Nueva notificación' };
    try {
        if (event.data) data = { ...data, ...event.data.json() };
    } catch { /* ignore */ }
    event.waitUntil(
        self.registration.showNotification(data.title, { body: data.body, icon: '/favicon.ico' })
    );
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    event.waitUntil(clients.openWindow('/'));
});
