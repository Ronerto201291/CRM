"use client";

import React, { useEffect, useState, useCallback } from "react";
import PageContainer from "@/components/PageContainer";

interface ConfirmingOp {
    id: string;
    bank: string;
    amount: number;
    fee: number;
    status: 'Pending' | 'Paid' | 'Cancelled';
    maturityDate: string;
    clientName?: string;
}

interface FactoringOp {
    id: string;
    clientName: string;
    amount: number;
    fee: number;
    status: 'Pending' | 'Paid' | 'Cancelled';
    maturityDate?: string;
}

interface CreditLine {
    id: string;
    bank: string;
    limit: number;
    drawn: number;
    available: number;
    interestRate?: number;
}

export default function FinancingPage() {
    const [confirming, setConfirming] = useState<ConfirmingOp[]>([]);
    const [factoring, setFactoring] = useState<FactoringOp[]>([]);
    const [creditLines, setCreditLines] = useState<CreditLine[]>([]);
    const [loading, setLoading] = useState(false);
    const [tab, setTab] = useState<'confirming' | 'factoring' | 'credit-lines'>('confirming');
    const [showCreate, setShowCreate] = useState(false);
    const [saving, setSaving] = useState(false);

    const [form, setForm] = useState<Record<string, string>>({});

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const [cRes, fRes, clRes] = await Promise.all([
                fetch('/api/proxy/v1/treasury/financing/confirming'),
                fetch('/api/proxy/v1/treasury/financing/factoring'),
                fetch('/api/proxy/v1/treasury/financing/credit-lines'),
            ]);
            if (cRes.ok) { const d = await cRes.json(); setConfirming(Array.isArray(d) ? d : (d.items ?? [])); }
            if (fRes.ok) { const d = await fRes.json(); setFactoring(Array.isArray(d) ? d : (d.items ?? [])); }
            if (clRes.ok) { const d = await clRes.json(); setCreditLines(Array.isArray(d) ? d : (d.items ?? [])); }
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => { load(); }, [load]);

    const submitConfirming = async () => {
        if (!form.bank || !form.amount) { alert('Banco y monto son obligatorios'); return; }
        setSaving(true);
        try {
            const res = await fetch('/api/proxy/v1/treasury/financing/confirming', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ bank: form.bank, amount: parseFloat(form.amount), fee: parseFloat(form.fee || '0'), maturityDate: form.maturityDate }),
            });
            if (res.ok) { setShowCreate(false); setForm({}); load(); }
            else { const e = await res.json(); alert(e.error || 'Error'); }
        } finally { setSaving(false); }
    };

    const submitFactoring = async () => {
        if (!form.clientName || !form.amount) { alert('Cliente y monto son obligatorios'); return; }
        setSaving(true);
        try {
            const res = await fetch('/api/proxy/v1/treasury/financing/factoring', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ clientName: form.clientName, amount: parseFloat(form.amount), fee: parseFloat(form.fee || '0') }),
            });
            if (res.ok) { setShowCreate(false); setForm({}); load(); }
            else { const e = await res.json(); alert(e.error || 'Error'); }
        } finally { setSaving(false); }
    };

    const submitCreditLine = async () => {
        if (!form.bank || !form.limit) { alert('Banco y límite son obligatorios'); return; }
        setSaving(true);
        try {
            const res = await fetch('/api/proxy/v1/treasury/financing/credit-lines', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ bank: form.bank, limit: parseFloat(form.limit), interestRate: parseFloat(form.interestRate || '0') }),
            });
            if (res.ok) { setShowCreate(false); setForm({}); load(); }
            else { const e = await res.json(); alert(e.error || 'Error'); }
        } finally { setSaving(false); }
    };

    const payOp = async (type: 'confirming' | 'factoring', id: string) => {
        const res = await fetch(`/api/proxy/v1/treasury/financing/${type}/${id}/pay`, { method: 'PATCH' });
        if (res.ok) { alert('Pagado'); load(); }
        else { const e = await res.json(); alert(e.error || 'Error'); }
    };

    const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;
    const statusBadge = (s: string) => <span className={`badge ${s === 'Paid' ? 'badge-success' : s === 'Pending' ? 'badge-warning' : 'badge-gray'}`}>{s}</span>;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Financiación</h1>
                    <p className="page-subtitle">Confirming, Factoring y Líneas de Crédito</p>
                </div>
                <button className="btn btn-primary" onClick={() => setShowCreate(true)}>+ Nueva Operación</button>
            </div>

            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px' }}>
                {(['confirming', 'factoring', 'credit-lines'] as const).map(t => (
                    <button key={t} onClick={() => setTab(t)}
                        style={{ padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '12px', fontWeight: tab === t ? 700 : 500, background: tab === t ? 'var(--brand-primary)' : 'var(--surface)', color: tab === t ? 'white' : 'var(--text-secondary)', cursor: 'pointer' }}>
                        {t === 'confirming' ? 'Confirming' : t === 'factoring' ? 'Factoring' : 'Líneas de Crédito'}
                    </button>
                ))}
            </div>

            {tab === 'confirming' && (
                <>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '14px', marginBottom: '20px' }}>
                        <MiniStat label="Total Confirming" value={fmt(confirming.reduce((s, c) => s + c.amount, 0))} color="var(--brand-primary)" />
                        <MiniStat label="Pendiente" value={fmt(confirming.filter(c => c.status === 'Pending').reduce((s, c) => s + c.amount, 0))} color="var(--warning)" />
                        <MiniStat label="Pagado" value={fmt(confirming.filter(c => c.status === 'Paid').reduce((s, c) => s + c.amount, 0))} color="var(--success)" />
                    </div>
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <table className="erp-table">
                            <thead><tr><th>Banco</th><th>Cliente</th><th style={{ textAlign: 'right' }}>Importe</th><th style={{ textAlign: 'right' }}>Comisión</th><th>Vencimiento</th><th>Estado</th><th>Acciones</th></tr></thead>
                            <tbody>
                                {confirming.length === 0 && <tr><td colSpan={7}><div className="empty-state"><div className="empty-state-icon">🏦</div><div className="empty-state-title">Sin operaciones de confirming</div></div></td></tr>}
                                {confirming.map(c => (
                                    <tr key={c.id}>
                                        <td style={{ fontWeight: 700 }}>{c.bank}</td>
                                        <td style={{ color: 'var(--text-secondary)' }}>{c.clientName || '—'}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(c.amount)}</td>
                                        <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>{fmt(c.fee)}</td>
                                        <td style={{ color: 'var(--text-muted)', fontSize: '12px' }}>{c.maturityDate ? new Date(c.maturityDate).toLocaleDateString('es-ES') : '—'}</td>
                                        <td>{statusBadge(c.status)}</td>
                                        <td>{c.status === 'Pending' && <button className="btn btn-secondary btn-sm" onClick={() => payOp('confirming', c.id)}>Pagar</button>}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </>
            )}

            {tab === 'factoring' && (
                <>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '14px', marginBottom: '20px' }}>
                        <MiniStat label="Total Factoring" value={fmt(factoring.reduce((s, f) => s + f.amount, 0))} color="var(--brand-primary)" />
                        <MiniStat label="Pendiente" value={fmt(factoring.filter(f => f.status === 'Pending').reduce((s, f) => s + f.amount, 0))} color="var(--warning)" />
                        <MiniStat label="Pagado" value={fmt(factoring.filter(f => f.status === 'Paid').reduce((s, f) => s + f.amount, 0))} color="var(--success)" />
                    </div>
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <table className="erp-table">
                            <thead><tr><th>Cliente</th><th style={{ textAlign: 'right' }}>Importe</th><th style={{ textAlign: 'right' }}>Comisión</th><th>Vencimiento</th><th>Estado</th><th>Acciones</th></tr></thead>
                            <tbody>
                                {factoring.length === 0 && <tr><td colSpan={6}><div className="empty-state"><div className="empty-state-icon">📃</div><div className="empty-state-title">Sin operaciones de factoring</div></div></td></tr>}
                                {factoring.map(f => (
                                    <tr key={f.id}>
                                        <td style={{ fontWeight: 700 }}>{f.clientName}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(f.amount)}</td>
                                        <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>{fmt(f.fee)}</td>
                                        <td style={{ color: 'var(--text-muted)', fontSize: '12px' }}>{f.maturityDate ? new Date(f.maturityDate).toLocaleDateString('es-ES') : '—'}</td>
                                        <td>{statusBadge(f.status)}</td>
                                        <td>{f.status === 'Pending' && <button className="btn btn-secondary btn-sm" onClick={() => payOp('factoring', f.id)}>Pagar</button>}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </>
            )}

            {tab === 'credit-lines' && (
                <>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '14px', marginBottom: '20px' }}>
                        <MiniStat label="Límite Total" value={fmt(creditLines.reduce((s, c) => s + c.limit, 0))} color="var(--brand-primary)" />
                        <MiniStat label="Dispuesto" value={fmt(creditLines.reduce((s, c) => s + c.drawn, 0))} color="var(--warning)" />
                        <MiniStat label="Disponible" value={fmt(creditLines.reduce((s, c) => s + c.available, 0))} color="var(--success)" />
                    </div>
                    <div className="erp-card" style={{ overflow: 'hidden' }}>
                        <table className="erp-table">
                            <thead><tr><th>Banco</th><th style={{ textAlign: 'right' }}>Límite</th><th style={{ textAlign: 'right' }}>Dispuesto</th><th style={{ textAlign: 'right' }}>Disponible</th><th style={{ textAlign: 'right' }}>Tipo Interés</th><th>Uso</th></tr></thead>
                            <tbody>
                                {creditLines.length === 0 && <tr><td colSpan={6}><div className="empty-state"><div className="empty-state-icon">💳</div><div className="empty-state-title">Sin líneas de crédito</div></div></td></tr>}
                                {creditLines.map(cl => (
                                    <tr key={cl.id}>
                                        <td style={{ fontWeight: 700 }}>{cl.bank}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(cl.limit)}</td>
                                        <td style={{ textAlign: 'right', color: 'var(--warning)' }}>{fmt(cl.drawn)}</td>
                                        <td style={{ textAlign: 'right', color: 'var(--success)', fontWeight: 700 }}>{fmt(cl.available)}</td>
                                        <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>{cl.interestRate ? `${cl.interestRate}%` : '—'}</td>
                                        <td>
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                <div style={{ flex: 1, height: '8px', background: 'var(--border)', borderRadius: '4px', overflow: 'hidden', maxWidth: '120px' }}>
                                                    <div style={{ width: `${(cl.drawn / cl.limit) * 100}%`, height: '100%', background: cl.drawn / cl.limit > 0.8 ? 'var(--danger)' : cl.drawn / cl.limit > 0.5 ? 'var(--warning)' : 'var(--success)', borderRadius: '4px' }} />
                                                </div>
                                                <span style={{ fontSize: '12px', fontWeight: 700 }}>{Math.round((cl.drawn / cl.limit) * 100)}%</span>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </>
            )}

            {showCreate && (
                <div className="modal-overlay" onClick={() => setShowCreate(false)}>
                    <div className="modal-box" onClick={e => e.stopPropagation()} style={{ maxWidth: '450px' }}>
                        <h2 style={{ marginBottom: '16px', fontSize: '18px', fontWeight: 700 }}>
                            Nueva {tab === 'confirming' ? 'Operación de Confirming' : tab === 'factoring' ? 'Operación de Factoring' : 'Línea de Crédito'}
                        </h2>
                        {tab === 'confirming' && (
                            <>
                                <div className="form-group"><label className="erp-label">BANCO *</label><input className="erp-input" value={form.bank || ''} onChange={e => setForm({ ...form, bank: e.target.value })} placeholder="Banco emisor" /></div>
                                <div className="form-group"><label className="erp-label">IMPORTE *</label><input type="number" className="erp-input" value={form.amount || ''} onChange={e => setForm({ ...form, amount: e.target.value })} placeholder="0.00" step="0.01" /></div>
                                <div className="form-group"><label className="erp-label">COMISIÓN</label><input type="number" className="erp-input" value={form.fee || ''} onChange={e => setForm({ ...form, fee: e.target.value })} placeholder="0.00" step="0.01" /></div>
                                <div className="form-group"><label className="erp-label">FECHA VENCIMIENTO</label><input type="date" className="erp-input" value={form.maturityDate || ''} onChange={e => setForm({ ...form, maturityDate: e.target.value })} /></div>
                                <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '20px' }}>
                                    <button className="btn btn-secondary" onClick={() => setShowCreate(false)}>Cancelar</button>
                                    <button className="btn btn-primary" onClick={submitConfirming} disabled={saving}>{saving ? 'Creando...' : 'Crear'}</button>
                                </div>
                            </>
                        )}
                        {tab === 'factoring' && (
                            <>
                                <div className="form-group"><label className="erp-label">NOMBRE CLIENTE *</label><input className="erp-input" value={form.clientName || ''} onChange={e => setForm({ ...form, clientName: e.target.value })} placeholder="Cliente" /></div>
                                <div className="form-group"><label className="erp-label">IMPORTE *</label><input type="number" className="erp-input" value={form.amount || ''} onChange={e => setForm({ ...form, amount: e.target.value })} placeholder="0.00" step="0.01" /></div>
                                <div className="form-group"><label className="erp-label">COMISIÓN</label><input type="number" className="erp-input" value={form.fee || ''} onChange={e => setForm({ ...form, fee: e.target.value })} placeholder="0.00" step="0.01" /></div>
                                <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '20px' }}>
                                    <button className="btn btn-secondary" onClick={() => setShowCreate(false)}>Cancelar</button>
                                    <button className="btn btn-primary" onClick={submitFactoring} disabled={saving}>{saving ? 'Creando...' : 'Crear'}</button>
                                </div>
                            </>
                        )}
                        {tab === 'credit-lines' && (
                            <>
                                <div className="form-group"><label className="erp-label">BANCO *</label><input className="erp-input" value={form.bank || ''} onChange={e => setForm({ ...form, bank: e.target.value })} placeholder="Banco" /></div>
                                <div className="form-group"><label className="erp-label">LÍMITE *</label><input type="number" className="erp-input" value={form.limit || ''} onChange={e => setForm({ ...form, limit: e.target.value })} placeholder="0.00" step="0.01" /></div>
                                <div className="form-group"><label className="erp-label">TIPO INTERÉS (%)</label><input type="number" className="erp-input" value={form.interestRate || ''} onChange={e => setForm({ ...form, interestRate: e.target.value })} placeholder="0.00" step="0.01" /></div>
                                <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '20px' }}>
                                    <button className="btn btn-secondary" onClick={() => setShowCreate(false)}>Cancelar</button>
                                    <button className="btn btn-primary" onClick={submitCreditLine} disabled={saving}>{saving ? 'Creando...' : 'Crear'}</button>
                                </div>
                            </>
                        )}
                    </div>
                </div>
            )}
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