'use client';
import { useState, useCallback } from 'react';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';

interface DeferredEntry {
    id: string;
    entryType: string;
    description: string;
    totalAmount: number;
    periodStart: string;
    periodEnd: string;
    recognizedAmount: number;
    remainingAmount: number;
    monthlyAmount: number;
    totalMonths: number;
    status: string;
    deferralAccountCode: string;
    counterpartAccountCode: string;
    createdAt: string;
}

const ENTRY_TYPES = [
    { value: 'PrepaidExpense', label: 'Gasto anticipado (480)', deferral: '480', counterpart: '600' },
    { value: 'DeferredRevenue', label: 'Ingreso diferido (485)', deferral: '485', counterpart: '700' },
];

const emptyForm = () => {
    const start = new Date();
    const end = new Date(start);
    end.setMonth(end.getMonth() + 11);
    return {
        entryType: 'PrepaidExpense',
        description: '',
        totalAmount: '',
        periodStart: start.toISOString().slice(0, 10),
        periodEnd: end.toISOString().slice(0, 10),
    };
};

export default function DeferredEntriesClient({ initialEntries }: { initialEntries: DeferredEntry[] }) {
    const [entries, setEntries] = useState(initialEntries);
    const [loading, setLoading] = useState(false);
    const [showModal, setShowModal] = useState(false);
    const [form, setForm] = useState(emptyForm());
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [recognizingId, setRecognizingId] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const res = await fetch('/api/proxy/deferred-entries?status=Active');
            const data = await res.json();
            setEntries(Array.isArray(data) ? data : []);
        } finally {
            setLoading(false);
        }
    }, []);

    const applyEntryType = (entryType: string) => {
        const preset = ENTRY_TYPES.find(t => t.value === entryType);
        setForm(f => ({ ...f, entryType }));
    };

    const handleCreate = async (e: React.FormEvent) => {
        e.preventDefault();
        const preset = ENTRY_TYPES.find(t => t.value === form.entryType);
        const amount = parseFloat(form.totalAmount);
        if (!form.description.trim() || Number.isNaN(amount) || amount <= 0) {
            setError('Descripción e importe positivo son obligatorios');
            return;
        }
        setSaving(true);
        setError(null);
        try {
            const res = await fetch('/api/proxy/deferred-entries', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    entryType: form.entryType,
                    description: form.description.trim(),
                    totalAmount: amount,
                    periodStart: form.periodStart,
                    periodEnd: form.periodEnd,
                    deferralAccountCode: preset?.deferral ?? '480',
                    counterpartAccountCode: preset?.counterpart ?? '600',
                }),
            });
            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                throw new Error(err.error || 'Error al crear periodificación');
            }
            setShowModal(false);
            setForm(emptyForm());
            await load();
        } catch (err: unknown) {
            setError(err instanceof Error ? err.message : 'Error');
        } finally {
            setSaving(false);
        }
    };

    const recognizeMonth = async (id: string) => {
        const now = new Date();
        setRecognizingId(id);
        try {
            const res = await fetch(`/api/proxy/deferred-entries/${id}/recognize`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ year: now.getFullYear(), month: now.getMonth() + 1 }),
            });
            if (res.ok) await load();
        } finally {
            setRecognizingId(null);
        }
    };

    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Periodificaciones</h1>
                    <p className="page-subtitle">Gastos anticipados (480) e ingresos diferidos (485)</p>
                </div>
                <button type="button" className="btn btn-primary" onClick={() => { setError(null); setShowModal(true); }}>
                    + Nueva periodificación
                </button>
            </div>

            {loading && <p style={{ color: 'var(--text-muted)' }}>Cargando...</p>}

            <div className="erp-card" style={{ overflowX: 'auto' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Descripción</th>
                            <th>Tipo</th>
                            <th>Periodo</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                            <th style={{ textAlign: 'right' }}>Reconocido</th>
                            <th style={{ textAlign: 'right' }}>Pendiente</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody>
                        {entries.length === 0 ? (
                            <tr><td colSpan={7} style={{ textAlign: 'center', color: 'var(--text-muted)' }}>Sin periodificaciones activas</td></tr>
                        ) : entries.map(e => (
                            <tr key={e.id}>
                                <td>{e.description}</td>
                                <td><span className="badge badge-gray">{e.entryType === 'PrepaidExpense' ? '480' : '485'}</span></td>
                                <td style={{ fontSize: '12px' }}>
                                    {new Date(e.periodStart).toLocaleDateString('es-ES')} — {new Date(e.periodEnd).toLocaleDateString('es-ES')}
                                    <div style={{ color: 'var(--text-muted)' }}>{e.totalMonths} meses · {fmt(e.monthlyAmount)}/mes</div>
                                </td>
                                <td style={{ textAlign: 'right' }}>{fmt(e.totalAmount)}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(e.recognizedAmount)}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(e.remainingAmount)}</td>
                                <td>
                                    <button
                                        type="button"
                                        className="btn btn-secondary"
                                        style={{ fontSize: '12px', padding: '4px 10px' }}
                                        disabled={recognizingId === e.id || e.remainingAmount <= 0}
                                        onClick={() => recognizeMonth(e.id)}
                                    >
                                        {recognizingId === e.id ? '...' : 'Reconocer mes'}
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <AccessibleModal open={showModal} onClose={() => setShowModal(false)} title="Nueva periodificación">
                <form onSubmit={handleCreate}>
                    {error && <div style={{ color: 'var(--danger)', marginBottom: 12, fontSize: 13 }}>{error}</div>}
                    <label className="erp-label">Tipo</label>
                    <select className="erp-input" value={form.entryType} onChange={ev => applyEntryType(ev.target.value)}>
                        {ENTRY_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                    </select>
                    <label className="erp-label" style={{ marginTop: 12 }}>Descripción</label>
                    <input className="erp-input" value={form.description} onChange={ev => setForm({ ...form, description: ev.target.value })} required />
                    <label className="erp-label" style={{ marginTop: 12 }}>Importe total (€)</label>
                    <input className="erp-input" type="number" step="0.01" min="0.01" value={form.totalAmount} onChange={ev => setForm({ ...form, totalAmount: ev.target.value })} required />
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginTop: 12 }}>
                        <div>
                            <label className="erp-label">Inicio periodo</label>
                            <input className="erp-input" type="date" value={form.periodStart} onChange={ev => setForm({ ...form, periodStart: ev.target.value })} required />
                        </div>
                        <div>
                            <label className="erp-label">Fin periodo</label>
                            <input className="erp-input" type="date" value={form.periodEnd} onChange={ev => setForm({ ...form, periodEnd: ev.target.value })} required />
                        </div>
                    </div>
                    <div style={{ display: 'flex', gap: 8, marginTop: 20, justifyContent: 'flex-end' }}>
                        <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                        <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Guardando...' : 'Crear'}</button>
                    </div>
                </form>
            </AccessibleModal>
        </PageContainer>
    );
}
