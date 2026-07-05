"use client";

import React, { useState, useCallback } from "react";
import PageContainer from "@/components/PageContainer";
import AccessibleModal from "@/components/AccessibleModal";

interface Currency {
    id: string;
    code: string;
    name: string;
    exchangeRateToEur: number;
    trend: 'up' | 'down' | 'stable';
    lastUpdated: string;
    isActive: boolean;
}

export default function CurrenciesClient({ initialCurrencies }: { initialCurrencies: Currency[] }) {
    const [currencies, setCurrencies] = useState<Currency[]>(initialCurrencies);
    const [loading, setLoading] = useState(false);
    const [showCreate, setShowCreate] = useState(false);
    const [form, setForm] = useState({ code: '', name: '', exchangeRateToEur: '' });
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);
    const [actionError, setActionError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const res = await fetch('/api/proxy/v1/treasury/currencies');
            if (res.ok) {
                const data = await res.json();
                setCurrencies(Array.isArray(data) ? data : (data.items ?? []));
            }
        } finally {
            setLoading(false);
        }
    }, []);

    const submit = async () => {
        setFormError(null);
        if (!form.code || !form.name) { setFormError('Código y nombre son obligatorios'); return; }
        setSaving(true);
        try {
            const rate = parseFloat(form.exchangeRateToEur);
            const res = await fetch('/api/proxy/v1/treasury/currencies', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ code: form.code.toUpperCase(), name: form.name, exchangeRateToEur: rate || 1 }),
            });
            if (res.ok) { setShowCreate(false); setForm({ code: '', name: '', exchangeRateToEur: '' }); load(); }
            else { const e = await res.json(); setFormError(e.error || 'Error'); }
        } finally { setSaving(false); }
    };

    const updateRate = async (id: string, newRate: number) => {
        setActionError(null);
        const res = await fetch(`/api/proxy/v1/treasury/currencies/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ exchangeRateToEur: newRate }),
        });
        if (res.ok) load();
        else { const e = await res.json(); setActionError(e.error || 'Error'); }
    };

    const toggleActive = async (id: string, isActive: boolean) => {
        setActionError(null);
        const method = isActive ? 'DELETE' : 'POST';
        const res = await fetch(`/api/proxy/v1/treasury/currencies/${id}`, { method });
        if (res.ok) load();
        else { const e = await res.json(); setActionError(e.error || 'Error'); }
    };

    const trendIcon = (trend: string) => trend === 'up' ? '▲' : trend === 'down' ? '▼' : '─';
    const trendColor = (trend: string) => trend === 'up' ? 'var(--success)' : trend === 'down' ? 'var(--danger)' : 'var(--text-muted)';

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Divisas</h1>
                    <p className="page-subtitle">Gestión de monedas y tipos de cambio</p>
                </div>
                <button className="btn btn-primary" onClick={() => setShowCreate(true)}>+ Nueva Divisa</button>
            </div>

            {actionError && (
                <div className="erp-card" style={{ padding: '12px 16px', marginBottom: 16, color: 'var(--danger)', background: 'var(--danger-bg)' }}>
                    {actionError}
                </div>
            )}

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '14px', marginBottom: '20px' }}>
                <MiniStat label="Divisas Activas" value={currencies.filter(c => c.isActive).length.toString()} color="var(--brand-primary)" />
                <MiniStat label="Divisas Inactivas" value={currencies.filter(c => !c.isActive).length.toString()} color="var(--text-muted)" />
                <MiniStat label="Última Actualización" value={currencies[0] ? new Date(currencies[0].lastUpdated).toLocaleDateString('es-ES') : '—'} color="var(--info)" />
            </div>

            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Código</th>
                            <th>Nombre</th>
                            <th style={{ textAlign: 'right' }}>Tasa vs EUR</th>
                            <th style={{ textAlign: 'center' }}>Tendencia</th>
                            <th>Última Actualización</th>
                            <th>Estado</th>
                            <th>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>
                        {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: '20px' }}>Cargando...</td></tr>}
                        {!loading && currencies.length === 0 && (
                            <tr><td colSpan={7}><div className="empty-state"><div className="empty-state-icon">💱</div><div className="empty-state-title">Sin divisas</div></div></td></tr>
                        )}
                        {currencies.map(c => (
                            <tr key={c.id} style={{ opacity: c.isActive ? 1 : 0.5 }}>
                                <td style={{ fontWeight: 800, fontSize: '16px', fontFamily: 'monospace', color: 'var(--brand-primary)' }}>{c.code}</td>
                                <td>{c.name}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{c.exchangeRateToEur.toFixed(4)}</td>
                                <td style={{ textAlign: 'center', color: trendColor(c.trend), fontWeight: 700 }}>{trendIcon(c.trend)}</td>
                                <td style={{ color: 'var(--text-muted)', fontSize: '12px' }}>{new Date(c.lastUpdated).toLocaleString('es-ES')}</td>
                                <td><span className={`badge ${c.isActive ? 'badge-success' : 'badge-gray'}`}>{c.isActive ? 'Activa' : 'Inactiva'}</span></td>
                                <td>
                                    <div style={{ display: 'flex', gap: '4px' }}>
                                        <button className="btn btn-secondary btn-sm" onClick={() => { const r = prompt('Nueva tasa vs EUR:', c.exchangeRateToEur.toString()); if (r) updateRate(c.id, parseFloat(r)); }}>✏️</button>
                                        <button className="btn btn-secondary btn-sm" onClick={() => toggleActive(c.id, !c.isActive)} style={{ color: c.isActive ? 'var(--danger)' : 'var(--success)' }}>
                                            {c.isActive ? 'Desactivar' : 'Activar'}
                                        </button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <AccessibleModal
                open={showCreate}
                onClose={() => setShowCreate(false)}
                title="Nueva Divisa"
                maxWidth="400px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary" onClick={() => setShowCreate(false)}>Cancelar</button>
                        <button className="btn btn-primary" onClick={submit} disabled={saving}>{saving ? 'Creando...' : 'Crear'}</button>
                    </div>
                )}
            >
                        {formError && (
                            <div style={{ padding: '10px 12px', marginBottom: 14, borderRadius: 8, color: 'var(--danger)', background: 'var(--danger-bg)', fontSize: 13 }}>
                                {formError}
                            </div>
                        )}
                        <div className="form-group">
                            <label className="erp-label">CÓDIGO (ISO 4217)</label>
                            <input className="erp-input" value={form.code} onChange={e => setForm({ ...form, code: e.target.value.toUpperCase() })} placeholder="USD" maxLength={3} />
                        </div>
                        <div className="form-group">
                            <label className="erp-label">NOMBRE</label>
                            <input className="erp-input" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="Dólar estadounidense" />
                        </div>
                        <div className="form-group">
                            <label className="erp-label">TASA vs EUR</label>
                            <input type="number" className="erp-input" value={form.exchangeRateToEur} onChange={e => setForm({ ...form, exchangeRateToEur: e.target.value })} placeholder="1.0000" step="0.0001" />
                        </div>
            </AccessibleModal>
        </PageContainer>
    );
}

function MiniStat({ label, value, color }: { label: string; value: string; color: string }) {
    return (
        <div className="erp-card" style={{ padding: '14px 16px' }}>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>{label}</div>
            <div style={{ fontSize: '18px', fontWeight: 800, color }}>{value}</div>
        </div>
    );
}