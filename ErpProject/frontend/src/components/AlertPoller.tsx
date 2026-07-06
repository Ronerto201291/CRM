'use client';
import { useEffect, useState, useCallback } from 'react';
import { addMinutesIso, isScheduledOverdue } from '@/lib/time';

interface Alert {
    id: string;
    title: string;
    description?: string;
    scheduledAt: string;
    clientName?: string;
    isAcknowledged: boolean;
    snoozedUntil?: string;
}

const SNOOZE_OPTIONS = [
    { label: '15 minutos', minutes: 15 },
    { label: '30 minutos', minutes: 30 },
    { label: '1 hora',     minutes: 60 },
    { label: '2 horas',    minutes: 120 },
    { label: 'Mañana',     minutes: 60 * 24 },
];

export default function AlertPoller() {
    const [queue, setQueue] = useState<Alert[]>([]);
    const [showSnooze, setShowSnooze] = useState(false);

    const current = queue[0] ?? null;

    const poll = useCallback(async () => {
        try {
            const res = await fetch('/api/proxy/crm/alerts/pending');
            if (res.ok) {
                const data: Alert[] = await res.json();
                // Add new alerts not already in queue
                setQueue(prev => {
                    const existingIds = new Set(prev.map(a => a.id));
                    const newOnes = data.filter(a => !existingIds.has(a.id));
                    return [...prev, ...newOnes];
                });
            }
        } catch { /* silently ignore network errors */ }
    }, []);

    useEffect(() => {
        const immediate = window.setTimeout(() => { void poll(); }, 0);
        const interval = setInterval(poll, 60_000);
        return () => {
            clearTimeout(immediate);
            clearInterval(interval);
        };
    }, [poll]);

    const acknowledge = async () => {
        if (!current) return;
        await fetch(`/api/proxy/crm/alerts/${current.id}/acknowledge`, { method: 'PATCH' });
        setQueue(q => q.slice(1));
        setShowSnooze(false);
    };

    const snooze = async (minutes: number) => {
        if (!current) return;
        const snoozedUntil = addMinutesIso(minutes);
        await fetch(`/api/proxy/crm/alerts/${current.id}/snooze`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ snoozedUntil }),
        });
        setQueue(q => q.slice(1));
        setShowSnooze(false);
    };

    if (!current) return null;

    const scheduledTime = new Date(current.scheduledAt).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' });
    const isOverdue = isScheduledOverdue(current.scheduledAt);

    return (
        <div style={{
            position: 'fixed', inset: 0, zIndex: 9999,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            background: 'rgba(0,0,0,0.5)', backdropFilter: 'blur(2px)',
        }}>
            <div style={{
                background: 'var(--bg-primary, #fff)',
                borderRadius: '14px',
                width: '100%',
                maxWidth: '420px',
                boxShadow: '0 20px 60px rgba(0,0,0,0.3)',
                overflow: 'hidden',
                fontFamily: 'Inter, sans-serif',
            }}>
                {/* Header */}
                <div style={{
                    background: isOverdue ? 'var(--danger, #ef4444)' : 'var(--brand-primary, #2563eb)',
                    padding: '20px 24px 16px',
                    color: '#fff',
                }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '4px' }}>
                        <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                            <path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                        </svg>
                        <span style={{ fontSize: '13px', fontWeight: 600, opacity: 0.9 }}>
                            {isOverdue ? 'Alerta vencida' : 'Recordatorio'}
                            {queue.length > 1 && (
                                <span style={{ marginLeft: '8px', background: 'rgba(255,255,255,0.25)', borderRadius: '99px', padding: '1px 8px', fontSize: '11px' }}>
                                    +{queue.length - 1} más
                                </span>
                            )}
                        </span>
                    </div>
                    <h2 style={{ fontSize: '20px', fontWeight: 800, margin: 0 }}>{current.title}</h2>
                </div>

                {/* Body */}
                <div style={{ padding: '20px 24px' }}>
                    <div style={{ display: 'flex', gap: '16px', marginBottom: current.description ? '12px' : 0 }}>
                        <div>
                            <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', letterSpacing: '0.05em', marginBottom: '2px' }}>PROGRAMADA PARA</div>
                            <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--text-primary)' }}>{scheduledTime}</div>
                        </div>
                        {current.clientName && (
                            <div>
                                <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', letterSpacing: '0.05em', marginBottom: '2px' }}>CLIENTE</div>
                                <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--brand-primary, #2563eb)' }}>{current.clientName}</div>
                            </div>
                        )}
                    </div>
                    {current.description && (
                        <p style={{ fontSize: '13px', color: 'var(--text-secondary)', margin: '12px 0 0', lineHeight: 1.5 }}>
                            {current.description}
                        </p>
                    )}
                </div>

                {/* Snooze picker */}
                {showSnooze && (
                    <div style={{ padding: '0 24px 16px', display: 'flex', flexDirection: 'column', gap: '6px' }}>
                        <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>Posponer hasta:</div>
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
                            {SNOOZE_OPTIONS.map(opt => (
                                <button
                                    key={opt.minutes}
                                    onClick={() => snooze(opt.minutes)}
                                    style={{
                                        padding: '6px 14px', borderRadius: '99px', border: '1px solid var(--border)',
                                        background: 'var(--bg-secondary)', color: 'var(--text-primary)',
                                        fontSize: '12px', fontWeight: 500, cursor: 'pointer',
                                    }}
                                >
                                    {opt.label}
                                </button>
                            ))}
                        </div>
                        <button
                            onClick={() => setShowSnooze(false)}
                            style={{ fontSize: '12px', color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', textAlign: 'left', marginTop: '4px' }}
                        >
                            Cancelar
                        </button>
                    </div>
                )}

                {/* Actions */}
                <div style={{
                    padding: '16px 24px 20px',
                    borderTop: '1px solid var(--border)',
                    display: 'flex', gap: '10px',
                }}>
                    {!showSnooze && (
                        <button
                            onClick={() => setShowSnooze(true)}
                            style={{
                                flex: 1, padding: '10px', borderRadius: '8px',
                                border: '1px solid var(--border)',
                                background: 'var(--bg-secondary)', color: 'var(--text-secondary)',
                                fontSize: '13px', fontWeight: 600, cursor: 'pointer',
                            }}
                        >
                            Posponer
                        </button>
                    )}
                    <button
                        onClick={acknowledge}
                        style={{
                            flex: 2, padding: '10px', borderRadius: '8px', border: 'none',
                            background: 'var(--brand-primary, #2563eb)', color: '#fff',
                            fontSize: '13px', fontWeight: 700, cursor: 'pointer',
                        }}
                    >
                        ✓ Aceptar
                    </button>
                </div>
            </div>
        </div>
    );
}
