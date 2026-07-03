"use client";

import React, { useState, useCallback } from "react";
import PageContainer from "@/components/PageContainer";
import AccessibleModal from "@/components/AccessibleModal";

interface Guarantee {
    id: string;
    type: 'Aval' | 'Caucion' | 'Deposito' | 'Otro';
    beneficiary: string;
    amount: number;
    currency: string;
    startDate: string;
    endDate: string;
    status: 'Active' | 'Expired' | 'Cancelled' | 'Claimed';
    autoRenew: boolean;
    bank?: string;
    documentRef?: string;
}

interface Collateral {
    id: string;
    type: string;
    description: string;
    value: number;
    relatedGuaranteeId?: string;
}

export default function GuaranteesClient({
    initialGuarantees,
    initialCollaterals,
}: {
    initialGuarantees: Guarantee[];
    initialCollaterals: Collateral[];
}) {
    const [guarantees, setGuarantees] = useState<Guarantee[]>(initialGuarantees);
    const [collaterals, setCollaterals] = useState<Collateral[]>(initialCollaterals);
    const [loading, setLoading] = useState(false);
    const [tab, setTab] = useState<'guarantees' | 'collateral'>('guarantees');
    const [showCreate, setShowCreate] = useState(false);
    const [form, setForm] = useState({ type: 'Aval', beneficiary: '', amount: '', currency: 'EUR', startDate: '', endDate: '', autoRenew: false, bank: '', documentRef: '' });
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);
    const [actionError, setActionError] = useState<string | null>(null);
    const [successMsg, setSuccessMsg] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const [gRes, cRes] = await Promise.all([
                fetch('/api/proxy/v1/treasury/guarantees'),
                fetch('/api/proxy/v1/treasury/guarantees/collateral'),
            ]);
            if (gRes.ok) { const d = await gRes.json(); setGuarantees(Array.isArray(d) ? d : (d.items ?? [])); }
            if (cRes.ok) { const d = await cRes.json(); setCollaterals(Array.isArray(d) ? d : (d.items ?? [])); }
        } finally {
            setLoading(false);
        }
    }, []);

    const submit = async () => {
        setFormError(null);
        if (!form.beneficiary || !form.amount) { setFormError('Beneficiario y monto son obligatorios'); return; }
        setSaving(true);
        try {
            const body = { ...form, amount: parseFloat(form.amount) };
            const res = await fetch('/api/proxy/v1/treasury/guarantees', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body),
            });
            if (res.ok) { setShowCreate(false); setForm({ type: 'Aval', beneficiary: '', amount: '', currency: 'EUR', startDate: '', endDate: '', autoRenew: false, bank: '', documentRef: '' }); load(); }
            else { const e = await res.json(); setFormError(e.error || 'Error'); }
        } finally { setSaving(false); }
    };

    const doAction = async (id: string, action: 'claim' | 'release') => {
        setActionError(null);
        setSuccessMsg(null);
        const res = await fetch(`/api/proxy/v1/treasury/guarantees/${id}/${action}`, { method: 'PATCH' });
        if (res.ok) { setSuccessMsg('Acción realizada'); load(); }
        else { const e = await res.json(); setActionError(e.error || 'Error'); }
    };

    const statusBadge = (status: string) => {
        const map: Record<string, string> = { Active: 'badge-success', Expired: 'badge-gray', Cancelled: 'badge-danger', Claimed: 'badge-warning' };
        return <span className={`badge ${map[status] ?? 'badge-gray'}`}>{status}</span>;
    };

    const daysUntilExpiry = (endDate: string) => {
        const days = Math.ceil((new Date(endDate).getTime() - Date.now()) / (1000 * 60 * 60 * 24));
        return days;
    };

    const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Garantías y Avales</h1>
                    <p className="page-subtitle">Gestión de avales, cauciones y garantías bancarias</p>
                </div>
                <button className="btn btn-primary" onClick={() => setShowCreate(true)}>+ Nueva Garantía</button>
            </div>

            {(actionError || successMsg) && (
                <div className="erp-card" style={{
                    padding: '12px 16px', marginBottom: 16,
                    color: actionError ? 'var(--danger)' : 'var(--success)',
                    background: actionError ? 'var(--danger-bg)' : 'var(--success-bg)',
                }}>
                    {actionError || successMsg}
                </div>
            )}

            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px' }}>
                <button onClick={() => setTab('guarantees')} style={{ padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '12px', fontWeight: tab === 'guarantees' ? 700 : 500, background: tab === 'guarantees' ? 'var(--brand-primary)' : 'var(--surface)', color: tab === 'guarantees' ? 'white' : 'var(--text-secondary)', cursor: 'pointer' }}>
                    Avales y Garantías ({guarantees.length})
                </button>
                <button onClick={() => setTab('collateral')} style={{ padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '12px', fontWeight: tab === 'collateral' ? 700 : 500, background: tab === 'collateral' ? 'var(--brand-primary)' : 'var(--surface)', color: tab === 'collateral' ? 'white' : 'var(--text-secondary)', cursor: 'pointer' }}>
                    Colaterales ({collaterals.length})
                </button>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '14px', marginBottom: '20px' }}>
                <MiniStat label="Total Garantías" value={fmt(guarantees.reduce((s, g) => s + g.amount, 0))} color="var(--brand-primary)" />
                <MiniStat label="Activas" value={guarantees.filter(g => g.status === 'Active').length.toString()} color="var(--success)" />
                <MiniStat label="Expiradas" value={guarantees.filter(g => g.status === 'Expired').length.toString()} color="var(--text-muted)" />
                <MiniStat label="Reclamadas" value={guarantees.filter(g => g.status === 'Claimed').length.toString()} color="var(--warning)" />
            </div>

            {tab === 'guarantees' && (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                <th>Tipo</th>
                                <th>Beneficiario</th>
                                <th>Entidad</th>
                                <th style={{ textAlign: 'right' }}>Importe</th>
                                <th>Inicio</th>
                                <th>Fin</th>
                                <th>Estado</th>
                                <th>Acciones</th>
                            </tr>
                        </thead>
                        <tbody>
                            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: '20px' }}>Cargando...</td></tr>}
                            {!loading && guarantees.length === 0 && <tr><td colSpan={8}><div className="empty-state"><div className="empty-state-icon">🛡️</div><div className="empty-state-title">Sin garantías</div></div></td></tr>}
                            {guarantees.map(g => {
                                const daysLeft = daysUntilExpiry(g.endDate);
                                return (
                                    <tr key={g.id}>
                                        <td style={{ fontWeight: 700 }}>{g.type}</td>
                                        <td>{g.beneficiary}</td>
                                        <td style={{ color: 'var(--text-muted)' }}>{g.bank || '—'}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(g.amount)}</td>
                                        <td style={{ color: 'var(--text-muted)', fontSize: '12px' }}>{new Date(g.startDate).toLocaleDateString('es-ES')}</td>
                                        <td>
                                            <span style={{ color: daysLeft < 30 ? 'var(--warning)' : 'var(--text-muted)', fontSize: '12px' }}>
                                                {new Date(g.endDate).toLocaleDateString('es-ES')}
                                                {daysLeft > 0 && <span style={{ marginLeft: '4px', fontWeight: 700, color: daysLeft < 30 ? 'var(--warning)' : 'var(--success)' }}>{daysLeft}d</span>}
                                                {daysLeft <= 0 && <span style={{ marginLeft: '4px', color: 'var(--danger)' }}>Expirado</span>}
                                            </span>
                                        </td>
                                        <td>{statusBadge(g.status)}</td>
                                        <td>
                                            {g.status === 'Active' && (
                                                <div style={{ display: 'flex', gap: '4px' }}>
                                                    <button className="btn btn-secondary btn-sm" onClick={() => doAction(g.id, 'claim')}>Reclamar</button>
                                                    <button className="btn btn-secondary btn-sm" onClick={() => doAction(g.id, 'release')} style={{ color: 'var(--success)' }}>Liberar</button>
                                                </div>
                                            )}
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}

            {tab === 'collateral' && (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                <th>Tipo</th>
                                <th>Descripción</th>
                                <th style={{ textAlign: 'right' }}>Valor</th>
                                <th>Garantía Vinculada</th>
                            </tr>
                        </thead>
                        <tbody>
                            {collaterals.length === 0 && <tr><td colSpan={4}><div className="empty-state"><div className="empty-state-icon">📋</div><div className="empty-state-title">Sin colaterales</div></div></td></tr>}
                            {collaterals.map(c => (
                                <tr key={c.id}>
                                    <td style={{ fontWeight: 700 }}>{c.type}</td>
                                    <td>{c.description}</td>
                                    <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(c.value)}</td>
                                    <td style={{ color: 'var(--text-muted)' }}>{c.relatedGuaranteeId ? 'Sí' : '—'}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            <AccessibleModal
                open={showCreate}
                onClose={() => setShowCreate(false)}
                title="Nueva Garantía"
                maxWidth="500px"
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
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                            <div className="form-group">
                                <label className="erp-label">TIPO</label>
                                <select className="erp-input" value={form.type} onChange={e => setForm({ ...form, type: e.target.value })}>
                                    <option>Aval</option>
                                    <option>Caucion</option>
                                    <option>Deposito</option>
                                    <option>Otro</option>
                                </select>
                            </div>
                            <div className="form-group">
                                <label className="erp-label">MONEDA</label>
                                <select className="erp-input" value={form.currency} onChange={e => setForm({ ...form, currency: e.target.value })}>
                                    <option>EUR</option>
                                    <option>USD</option>
                                    <option>GBP</option>
                                </select>
                            </div>
                        </div>
                        <div className="form-group">
                            <label className="erp-label">BENEFICIARIO *</label>
                            <input className="erp-input" value={form.beneficiary} onChange={e => setForm({ ...form, beneficiary: e.target.value })} placeholder="Nombre del beneficiario" />
                        </div>
                        <div className="form-group">
                            <label className="erp-label">IMPORTE *</label>
                            <input type="number" className="erp-input" value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} placeholder="0.00" step="0.01" />
                        </div>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                            <div className="form-group">
                                <label className="erp-label">FECHA INICIO</label>
                                <input type="date" className="erp-input" value={form.startDate} onChange={e => setForm({ ...form, startDate: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">FECHA FIN</label>
                                <input type="date" className="erp-input" value={form.endDate} onChange={e => setForm({ ...form, endDate: e.target.value })} />
                            </div>
                        </div>
                        <div className="form-group">
                            <label className="erp-label">ENTIDAD BANCARIA</label>
                            <input className="erp-input" value={form.bank} onChange={e => setForm({ ...form, bank: e.target.value })} placeholder="Banco emisor" />
                        </div>
                        <div className="form-group">
                            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                                <input type="checkbox" checked={form.autoRenew} onChange={e => setForm({ ...form, autoRenew: e.target.checked })} />
                                <span style={{ fontSize: '13px' }}>Renovación automática</span>
                            </label>
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