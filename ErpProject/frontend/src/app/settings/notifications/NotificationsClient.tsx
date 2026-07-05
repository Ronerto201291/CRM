'use client';
import { useEffect, useRef, useState } from 'react';
import PageContainer from '@/components/PageContainer';

function urlBase64ToUint8Array(base64String: string) {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const raw = atob(base64);
    return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
}

export default function NotificationsClient() {
    const [frequency, setFrequency] = useState('daily');
    const [pushEnabled, setPushEnabled] = useState(false);
    const [pushPublicKey, setPushPublicKey] = useState<string | null>(null);
    const [message, setMessage] = useState<string | null>(null);
    const swRegistered = useRef(false);

    useEffect(() => {
        fetch('/api/proxy/notifications/settings').then(async r => {
            if (r.ok) {
                const d = await r.json();
                setFrequency(d.frequency ?? 'daily');
                setPushEnabled(d.pushEnabled ?? false);
                setPushPublicKey(d.pushPublicKey ?? null);
            }
        });
    }, []);

    useEffect(() => {
        if (pushEnabled && 'serviceWorker' in navigator && !swRegistered.current) {
            swRegistered.current = true;
            navigator.serviceWorker.register('/sw.js').catch(() => {});
        }
    }, [pushEnabled]);

    const save = async () => {
        const r = await fetch('/api/proxy/notifications/settings', {
            method: 'PUT', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ frequency }),
        });
        const d = await r.json().catch(() => ({}));
        setMessage(r.ok ? d.message : d.error || 'Error');
    };

    const subscribePush = async () => {
        if (!pushPublicKey || !('serviceWorker' in navigator) || !('PushManager' in window)) {
            setMessage('Push no disponible en este navegador.');
            return;
        }
        try {
            const permission = await Notification.requestPermission();
            if (permission !== 'granted') {
                setMessage('Permiso de notificaciones denegado.');
                return;
            }
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(pushPublicKey),
            });
            const json = sub.toJSON();
            const r = await fetch('/api/proxy/notifications/push/subscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    endpoint: json.endpoint,
                    p256dh: json.keys?.p256dh,
                    auth: json.keys?.auth,
                }),
            });
            const d = await r.json().catch(() => ({}));
            setMessage(r.ok ? d.message : d.error || 'Error al suscribir push');
        } catch {
            setMessage('Error al activar notificaciones push.');
        }
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Notificaciones proactivas</h1>
                    <p className="page-subtitle">Email + push del navegador (#42)</p>
                </div>
            </div>
            {message && <div className="erp-card" style={{ padding: '12px', marginBottom: '16px' }}>{message}</div>}
            <div className="erp-card" style={{ padding: '20px', maxWidth: '480px', marginBottom: '16px' }}>
                <label className="erp-label">FRECUENCIA DE EMAILS</label>
                <select className="erp-input" value={frequency} onChange={e => setFrequency(e.target.value)}>
                    <option value="daily">Diaria</option>
                    <option value="weekly">Semanal (lunes)</option>
                    <option value="disabled">Desactivada</option>
                </select>
                <button className="btn btn-primary" style={{ marginTop: '16px' }} onClick={save}>Guardar</button>
            </div>
            {pushEnabled && (
                <div className="erp-card" style={{ padding: '20px', maxWidth: '480px' }}>
                    <h2 style={{ fontSize: '15px', fontWeight: 700, marginBottom: '8px' }}>Notificaciones push</h2>
                    <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '12px' }}>
                        Recibe alertas en el navegador además del email.
                    </p>
                    <button className="btn btn-secondary" onClick={subscribePush}>Activar push en este navegador</button>
                </div>
            )}
        </PageContainer>
    );
}
