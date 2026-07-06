"use client";

import React, { useState, useCallback } from "react";
import PageContainer from "@/components/PageContainer";
import AccessibleModal from "@/components/AccessibleModal";
import { consolidationGroupSchema, consolidationSubsidiarySchema } from '@/lib/schemas/treasuryFormSchemas';

interface Subsidiary {
    id: string;
    name: string;
    participationPct: number;
    currency: string;
    balanceSheet?: { totalAssets: number; totalLiabilities: number; netWorth: number };
}

interface ConsolidationGroup {
    id: string;
    name: string;
    currency: string;
    totalAssets: number;
    subsidiaries: Subsidiary[];
}

interface ConsolidationClientProps {
    initialGroups: ConsolidationGroup[];
}

export default function ConsolidationClient({ initialGroups }: ConsolidationClientProps) {
    const [groups, setGroups] = useState<ConsolidationGroup[]>(initialGroups);
    const [loading, setLoading] = useState(false);
    const [showCreateGroup, setShowCreateGroup] = useState(false);
    const [showCreateSubsidiary, setShowCreateSubsidiary] = useState<string | null>(null);
    const [form, setForm] = useState({ name: '', currency: 'EUR' });
    const [subForm, setSubForm] = useState({ name: '', participationPct: '', currency: 'EUR' });
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);
    const [actionError, setActionError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const res = await fetch('/api/proxy/v1/treasury/consolidation');
            if (res.ok) { const d = await res.json(); setGroups(Array.isArray(d) ? d : (d.items ?? [])); }
        } finally {
            setLoading(false);
        }
    }, []);

    const createGroup = async () => {
        setFormError(null);
        const parsed = consolidationGroupSchema.safeParse(form);
        if (!parsed.success) {
            setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
            return;
        }
        setSaving(true);
        try {
            const res = await fetch('/api/proxy/v1/treasury/consolidation', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name: form.name, currency: form.currency }),
            });
            if (res.ok) { setShowCreateGroup(false); setForm({ name: '', currency: 'EUR' }); load(); }
            else { const e = await res.json(); setFormError(e.error || 'Error'); }
        } finally { setSaving(false); }
    };

    const createSubsidiary = async (groupId: string) => {
        setActionError(null);
        const parsed = consolidationSubsidiarySchema.safeParse(subForm);
        if (!parsed.success) {
            setActionError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
            return;
        }
        setSaving(true);
        try {
            const res = await fetch(`/api/proxy/v1/treasury/consolidation/${groupId}/subsidiaries`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name: subForm.name, participationPct: parseFloat(subForm.participationPct), currency: subForm.currency }),
            });
            if (res.ok) { setShowCreateSubsidiary(null); setSubForm({ name: '', participationPct: '', currency: 'EUR' }); load(); }
            else { const e = await res.json(); setActionError(e.error || 'Error'); }
        } finally { setSaving(false); }
    };

    const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Consolidación de Grupos</h1>
                    <p className="page-subtitle">Grupos empresariales y filiales para consolidación contable</p>
                </div>
                <button className="btn btn-primary" onClick={() => setShowCreateGroup(true)}>+ Nuevo Grupo</button>
            </div>

            {actionError && (
                <div className="erp-card" style={{ padding: '12px 16px', marginBottom: 16, color: 'var(--danger)', background: 'var(--danger-bg)' }}>
                    {actionError}
                </div>
            )}

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '14px', marginBottom: '20px' }}>
                <MiniStat label="Grupos" value={groups.length.toString()} color="var(--brand-primary)" />
                <MiniStat label="Total Filiales" value={groups.reduce((s, g) => s + g.subsidiaries.length, 0).toString()} color="var(--info)" />
                <MiniStat label="Activos Totales Consolidados" value={fmt(groups.reduce((s, g) => s + g.totalAssets, 0))} color="var(--success)" />
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                {loading && <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>Cargando...</div>}
                {!loading && groups.length === 0 && (
                    <div className="erp-card">
                        <div className="empty-state">
                            <div className="empty-state-icon">🏢</div>
                            <div className="empty-state-title">Sin grupos de consolidación</div>
                            <div className="empty-state-sub">Crea tu primer grupo para gestionar filiales</div>
                        </div>
                    </div>
                )}
                {groups.map(group => (
                    <div key={group.id} className="erp-card" style={{ overflow: 'hidden' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px', borderBottom: '1px solid var(--border)', background: 'var(--surface-2)' }}>
                            <div>
                                <div style={{ fontSize: '16px', fontWeight: 800 }}>{group.name}</div>
                                <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Moneda: {group.currency} · {group.subsidiaries.length} filial(es)</div>
                            </div>
                            <div style={{ textAlign: 'right' }}>
                                <div style={{ fontSize: '18px', fontWeight: 800, color: 'var(--brand-primary)' }}>{fmt(group.totalAssets)}</div>
                                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Activos consolidados</div>
                            </div>
                        </div>

                        <table className="erp-table">
                            <thead>
                                <tr>
                                    <th>Filial</th>
                                    <th style={{ textAlign: 'right' }}>Participación</th>
                                    <th>Moneda</th>
                                    <th style={{ textAlign: 'right' }}>Activos</th>
                                    <th style={{ textAlign: 'right' }}>Pasivos</th>
                                    <th style={{ textAlign: 'right' }}>Patrimonio</th>
                                </tr>
                            </thead>
                            <tbody>
                                {group.subsidiaries.length === 0 && (
                                    <tr><td colSpan={6} style={{ textAlign: 'center', padding: '16px', color: 'var(--text-muted)' }}>Sin filiales</td></tr>
                                )}
                                {group.subsidiaries.map(sub => (
                                    <tr key={sub.id}>
                                        <td style={{ fontWeight: 600 }}>{sub.name}</td>
                                        <td style={{ textAlign: 'right' }}>
                                            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: '8px' }}>
                                                <span style={{ fontWeight: 800, color: 'var(--brand-primary)' }}>{sub.participationPct}%</span>
                                                <div style={{ width: '60px', height: '6px', background: 'var(--border)', borderRadius: '3px', overflow: 'hidden' }}>
                                                    <div style={{ width: `${sub.participationPct}%`, height: '100%', background: 'var(--brand-primary)', borderRadius: '3px' }} />
                                                </div>
                                            </div>
                                        </td>
                                        <td style={{ color: 'var(--text-muted)' }}>{sub.currency}</td>
                                        <td style={{ textAlign: 'right' }}>{sub.balanceSheet ? fmt(sub.balanceSheet.totalAssets) : '—'}</td>
                                        <td style={{ textAlign: 'right' }}>{sub.balanceSheet ? fmt(sub.balanceSheet.totalLiabilities) : '—'}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 700 }}>{sub.balanceSheet ? fmt(sub.balanceSheet.netWorth) : '—'}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>

                        <div style={{ padding: '12px 16px', borderTop: '1px solid var(--border)', background: 'var(--surface)' }}>
                            <button className="btn btn-secondary btn-sm" onClick={() => setShowCreateSubsidiary(group.id)}>+ Añadir Filial</button>
                        </div>
                    </div>
                ))}
            </div>

            <AccessibleModal open={showCreateGroup} onClose={() => setShowCreateGroup(false)} title="Nuevo Grupo de Consolidación" maxWidth="400px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowCreateGroup(false)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={createGroup} disabled={saving}>{saving ? 'Creando...' : 'Crear'}</button>
                </div>)}>
                        {formError && (
                            <div style={{ padding: '10px 12px', marginBottom: 14, borderRadius: 8, color: 'var(--danger)', background: 'var(--danger-bg)', fontSize: 13 }}>
                                {formError}
                            </div>
                        )}
                        <div className="form-group">
                            <label className="erp-label">NOMBRE *</label>
                            <input className="erp-input" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="Grupo empresarial" />
                        </div>
                        <div className="form-group">
                            <label className="erp-label">MONEDA</label>
                            <select className="erp-input" value={form.currency} onChange={e => setForm({ ...form, currency: e.target.value })}>
                                <option>EUR</option>
                                <option>USD</option>
                                <option>GBP</option>
                            </select>
                        </div>
            </AccessibleModal>

            <AccessibleModal open={!!showCreateSubsidiary} onClose={() => setShowCreateSubsidiary(null)} title="Nueva Filial" maxWidth="400px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowCreateSubsidiary(null)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={() => showCreateSubsidiary && createSubsidiary(showCreateSubsidiary)} disabled={saving}>{saving ? 'Creando...' : 'Crear'}</button>
                </div>)}>
                        <div className="form-group">
                            <label className="erp-label">NOMBRE *</label>
                            <input className="erp-input" value={subForm.name} onChange={e => setSubForm({ ...subForm, name: e.target.value })} placeholder="Nombre de la filial" />
                        </div>
                        <div className="form-group">
                            <label className="erp-label">% PARTICIPACIÓN *</label>
                            <input type="number" className="erp-input" value={subForm.participationPct} onChange={e => setSubForm({ ...subForm, participationPct: e.target.value })} placeholder="100" min="0.01" max="100" step="0.01" />
                        </div>
                        <div className="form-group">
                            <label className="erp-label">MONEDA</label>
                            <select className="erp-input" value={subForm.currency} onChange={e => setSubForm({ ...subForm, currency: e.target.value })}>
                                <option>EUR</option>
                                <option>USD</option>
                                <option>GBP</option>
                            </select>
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