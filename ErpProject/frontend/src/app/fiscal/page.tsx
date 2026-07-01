'use client';
import { useEffect, useState } from 'react';
import PageContainer from '@/components/PageContainer';

interface FiscalEvent {
    id: string;
    modelCode: string;
    modelName: string;
    year: number;
    quarter?: number;
    month?: number;
    deadlineDate: string;
    reminderDate: string;
    status: string;
    submittedAt?: string;
    submissionReference?: string;
    amount?: number;
    notes?: string;
    diasRestantes: number;
    isOverdue: boolean;
}

const STATUS_LABEL: Record<string, string> = {
    Pending: 'Pendiente',
    Reminded: 'Recordado',
    InProgress: 'En trámite',
    Submitted: 'Presentado',
    Paid: 'Pagado',
    Missed: 'Incumplido',
};

const STATUS_BADGE: Record<string, string> = {
    Pending: 'badge-warning',
    Reminded: 'badge-info',
    InProgress: 'badge-info',
    Submitted: 'badge-success',
    Paid: 'badge-success',
    Missed: 'badge-danger',
};

const MODEL_INFO: Record<string, { desc: string; color: string }> = {
    '303': { desc: 'IVA trimestral', color: '#3b82f6' },
    '390': { desc: 'Resumen anual IVA', color: '#8b5cf6' },
    '347': { desc: 'Operaciones con terceros', color: '#f59e0b' },
    '349': { desc: 'Intracomunitarias', color: '#06b6d4' },
    '111': { desc: 'Retenciones IRPF', color: '#ec4899' },
    '190': { desc: 'Resumen anual retenciones', color: '#d946ef' },
    '130': { desc: 'Pago fraccionado IS', color: '#10b981' },
    '100': { desc: 'Impuesto Sociedades', color: '#ef4444' },
    '180': { desc: 'Arrendamientos', color: '#f97316' },
};

const MONTHS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

export default function FiscalPage() {
    const [events, setEvents] = useState<FiscalEvent[]>([]);
    const [loading, setLoading] = useState(true);
    const [year, setYear] = useState(new Date().getFullYear());
    const [filter, setFilter] = useState<'all' | 'pending' | 'overdue' | 'submitted'>('all');
    const [showModal, setShowModal] = useState(false);
    const [showSubmitModal, setShowSubmitModal] = useState<FiscalEvent | null>(null);
    const [submitRef, setSubmitRef] = useState('');
    const [form, setForm] = useState({ modelCode: '303', modelName: '', year: new Date().getFullYear(), quarter: '', month: '', deadlineDate: '', reminderDate: '', amount: '', notes: '' });
    const [saving, setSaving] = useState(false);

    const load = async () => {
        setLoading(true);
        const r = await fetch(`/api/proxy/fiscal/calendar?year=${year}`);
        if (r.ok) setEvents(await r.json());
        setLoading(false);
    };

    useEffect(() => { load(); }, [year]);

    const filtered = events.filter(e => {
        if (filter === 'pending') return e.status === 'Pending' || e.status === 'Reminded' || e.status === 'InProgress';
        if (filter === 'overdue') return e.isOverdue;
        if (filter === 'submitted') return e.status === 'Submitted' || e.status === 'Paid';
        return true;
    });

    const submitEvent = async () => {
        if (!showSubmitModal) return;
        setSaving(true);
        await fetch(`/api/proxy/fiscal/calendar/${showSubmitModal.id}/submit`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ submissionReference: submitRef }),
        });
        setSaving(false);
        setShowSubmitModal(null);
        setSubmitRef('');
        load();
    };

    const createEvent = async () => {
        setSaving(true);
        await fetch('/api/proxy/fiscal/calendar/events', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                ...form,
                year: parseInt(String(form.year)),
                quarter: form.quarter ? parseInt(form.quarter) : null,
                month: form.month ? parseInt(form.month) : null,
                amount: form.amount ? parseFloat(form.amount) : null,
            }),
        });
        setSaving(false);
        setShowModal(false);
        load();
    };

    const fmt = (n?: number) => n != null ? `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}` : '—';
    const fmtDate = (d: string) => new Date(d).toLocaleDateString('es-ES', { day: '2-digit', month: 'short', year: 'numeric' });

    const pending = events.filter(e => e.status === 'Pending' || e.status === 'Reminded' || e.status === 'InProgress');
    const overdue = events.filter(e => e.isOverdue);
    const submitted = events.filter(e => e.status === 'Submitted' || e.status === 'Paid');
    const upcoming = events.filter(e => e.diasRestantes >= 0 && e.diasRestantes <= 30 && e.status !== 'Submitted' && e.status !== 'Paid');

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Calendario Fiscal</h1>
                    <p className="page-subtitle">Obligaciones tributarias AEAT — España</p>
                </div>
                <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                    <select className="erp-input" style={{ margin: 0, width: '100px' }} value={year} onChange={e => setYear(parseInt(e.target.value))}>
                        {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
                    </select>
                    <button className="btn btn-primary" onClick={() => setShowModal(true)}>+ Evento manual</button>
                </div>
            </div>

            {/* KPIs */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                {[
                    { label: 'Pendientes', value: pending.length, color: 'var(--warning)', bg: '#fffbeb', onClick: () => setFilter('pending') },
                    { label: 'Vencidos', value: overdue.length, color: 'var(--danger)', bg: 'var(--danger-bg)', onClick: () => setFilter('overdue') },
                    { label: 'Próximos 30 días', value: upcoming.length, color: 'var(--brand-primary)', bg: 'var(--brand-light, #eff6ff)', onClick: () => setFilter('pending') },
                    { label: 'Presentados', value: submitted.length, color: 'var(--success)', bg: 'var(--success-bg)', onClick: () => setFilter('submitted') },
                ].map(k => (
                    <div key={k.label} className="erp-card" style={{ padding: '16px 20px', cursor: 'pointer', transition: 'all 0.15s' }} onClick={k.onClick}>
                        <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>{k.label}</div>
                        <div style={{ fontSize: '28px', fontWeight: 900, color: k.color, marginTop: '4px' }}>{k.value}</div>
                    </div>
                ))}
            </div>

            {/* Alert: upcoming events in 7 days */}
            {events.filter(e => e.diasRestantes >= 0 && e.diasRestantes <= 7 && e.status !== 'Submitted' && e.status !== 'Paid').map(e => (
                <div key={e.id} style={{ marginBottom: '10px', padding: '12px 16px', borderRadius: '8px', background: e.diasRestantes <= 3 ? 'var(--danger-bg)' : '#fffbeb', color: e.diasRestantes <= 3 ? 'var(--danger)' : '#92400e', fontSize: '13px', fontWeight: 600, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span>{e.diasRestantes <= 3 ? '🚨' : '⚠️'} <strong>{e.modelName}</strong> vence el {fmtDate(e.deadlineDate)} — {e.diasRestantes === 0 ? 'HOY' : `${e.diasRestantes} días`}</span>
                    <button className="btn btn-sm" style={{ background: e.diasRestantes <= 3 ? 'var(--danger)' : '#d97706', color: '#fff', border: 'none' }} onClick={() => { setShowSubmitModal(e); setSubmitRef(''); }}>Marcar presentado</button>
                </div>
            ))}

            {/* Filter tabs */}
            <div style={{ display: 'flex', gap: '4px', marginBottom: '20px', borderBottom: '1px solid var(--border)' }}>
                {([['all', 'Todos'], ['pending', 'Pendientes'], ['overdue', 'Vencidos'], ['submitted', 'Presentados']] as [typeof filter, string][]).map(([key, label]) => (
                    <button key={key} onClick={() => setFilter(key)} style={{
                        padding: '8px 16px', border: 'none', cursor: 'pointer', fontSize: '13px', fontWeight: 600,
                        background: 'none', borderBottom: `2px solid ${filter === key ? 'var(--brand-primary)' : 'transparent'}`,
                        color: filter === key ? 'var(--brand-primary)' : 'var(--text-secondary)',
                    }}>{label} {key === 'all' ? `(${events.length})` : key === 'pending' ? `(${pending.length})` : key === 'overdue' ? `(${overdue.length})` : `(${submitted.length})`}</button>
                ))}
            </div>

            {/* Events table */}
            {loading ? (
                <div className="erp-card" style={{ padding: '48px', textAlign: 'center', color: 'var(--text-muted)' }}>Cargando calendario fiscal...</div>
            ) : filtered.length === 0 ? (
                <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                    <div style={{ fontSize: '36px', marginBottom: '12px' }}>📅</div>
                    <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>No hay eventos para este filtro.</p>
                </div>
            ) : (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead><tr>
                            <th>Modelo</th>
                            <th>Obligación</th>
                            <th>Período</th>
                            <th>Fecha límite</th>
                            <th style={{ textAlign: 'right' }}>Días</th>
                            <th style={{ textAlign: 'right' }}>Importe</th>
                            <th>Estado</th>
                            <th style={{ textAlign: 'right' }}>Acción</th>
                        </tr></thead>
                        <tbody>
                            {filtered.sort((a, b) => new Date(a.deadlineDate).getTime() - new Date(b.deadlineDate).getTime()).map(e => {
                                const info = MODEL_INFO[e.modelCode];
                                const daysColor = e.isOverdue ? 'var(--danger)' : e.diasRestantes <= 7 ? '#d97706' : 'var(--text-secondary)';
                                const period = e.quarter ? `T${e.quarter} ${e.year}` : e.month ? `${MONTHS[e.month - 1]} ${e.year}` : `${e.year}`;
                                return (
                                    <tr key={e.id}>
                                        <td>
                                            <span style={{ display: 'inline-block', padding: '3px 8px', borderRadius: '6px', fontSize: '12px', fontWeight: 800, background: info ? info.color + '20' : 'var(--surface-2)', color: info ? info.color : 'var(--text-primary)', fontFamily: 'monospace', letterSpacing: '0.05em' }}>
                                                {e.modelCode}
                                            </span>
                                        </td>
                                        <td>
                                            <div style={{ fontWeight: 600, fontSize: '13px' }}>{e.modelName}</div>
                                            {info && <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{info.desc}</div>}
                                        </td>
                                        <td style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{period}</td>
                                        <td style={{ fontSize: '13px', fontWeight: e.isOverdue || e.diasRestantes <= 7 ? 700 : 400, color: daysColor }}>{fmtDate(e.deadlineDate)}</td>
                                        <td style={{ textAlign: 'right', fontWeight: 700, color: daysColor, fontSize: '14px' }}>
                                            {e.isOverdue ? `+${Math.abs(e.diasRestantes)}` : e.diasRestantes === 0 ? 'HOY' : e.diasRestantes}
                                        </td>
                                        <td style={{ textAlign: 'right', fontSize: '13px' }}>{fmt(e.amount)}</td>
                                        <td>
                                            <span className={`badge ${STATUS_BADGE[e.status] || 'badge-gray'}`}>{STATUS_LABEL[e.status] || e.status}</span>
                                            {e.submissionReference && <div style={{ fontSize: '10px', color: 'var(--text-muted)', marginTop: '2px' }}>Ref: {e.submissionReference}</div>}
                                        </td>
                                        <td style={{ textAlign: 'right' }}>
                                            {(e.status === 'Pending' || e.status === 'Reminded' || e.status === 'InProgress') && (
                                                <button className="btn btn-secondary btn-sm" onClick={() => { setShowSubmitModal(e); setSubmitRef(''); }}>Presentar</button>
                                            )}
                                            {(e.status === 'Submitted' || e.status === 'Paid') && e.submittedAt && (
                                                <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{fmtDate(e.submittedAt)}</span>
                                            )}
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}

            {/* ── MODAL: Marcar como presentado ── */}
            {showSubmitModal && (
                <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget) setShowSubmitModal(null); }}>
                    <div className="modal-box" style={{ maxWidth: '440px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                            <h2 style={{ fontSize: '18px', fontWeight: 700 }}>Marcar como Presentado</h2>
                            <button onClick={() => setShowSubmitModal(null)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}>✕</button>
                        </div>
                        <p style={{ fontSize: '13px', color: 'var(--text-secondary)', marginBottom: '16px' }}>
                            <strong>{showSubmitModal.modelName}</strong> — Vence el {fmtDate(showSubmitModal.deadlineDate)}
                        </p>
                        <div className="form-group">
                            <label className="erp-label">Nº REFERENCIA AEAT (opcional)</label>
                            <input className="erp-input" value={submitRef} onChange={e => setSubmitRef(e.target.value)} placeholder="NRGP o referencia del modelo" />
                        </div>
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '20px' }}>
                            <button className="btn btn-secondary" onClick={() => setShowSubmitModal(null)}>Cancelar</button>
                            <button className="btn btn-primary" onClick={submitEvent} disabled={saving}>{saving ? 'Guardando...' : '✓ Confirmar presentación'}</button>
                        </div>
                    </div>
                </div>
            )}

            {/* ── MODAL: Nuevo evento manual ── */}
            {showModal && (
                <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget) setShowModal(false); }}>
                    <div className="modal-box" style={{ maxWidth: '520px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                            <h2 style={{ fontSize: '18px', fontWeight: 700 }}>Nuevo Evento Fiscal Manual</h2>
                            <button onClick={() => setShowModal(false)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}>✕</button>
                        </div>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group">
                                <label className="erp-label">MODELO *</label>
                                <select className="erp-input" value={form.modelCode} onChange={e => setForm({ ...form, modelCode: e.target.value, modelName: MODEL_INFO[e.target.value]?.desc || form.modelName })}>
                                    {Object.entries(MODEL_INFO).map(([code, info]) => <option key={code} value={code}>{code} — {info.desc}</option>)}
                                    <option value="OTRO">Otro</option>
                                </select>
                            </div>
                            <div className="form-group">
                                <label className="erp-label">AÑO *</label>
                                <select className="erp-input" value={form.year} onChange={e => setForm({ ...form, year: parseInt(e.target.value) })}>
                                    {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
                                </select>
                            </div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">DESCRIPCIÓN *</label>
                                <input className="erp-input" value={form.modelName} onChange={e => setForm({ ...form, modelName: e.target.value })} placeholder="IVA trimestral T1 2026" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">TRIMESTRE</label>
                                <select className="erp-input" value={form.quarter} onChange={e => setForm({ ...form, quarter: e.target.value, month: '' })}>
                                    <option value="">—</option>
                                    <option value="1">T1</option><option value="2">T2</option>
                                    <option value="3">T3</option><option value="4">T4</option>
                                </select>
                            </div>
                            <div className="form-group">
                                <label className="erp-label">MES</label>
                                <select className="erp-input" value={form.month} onChange={e => setForm({ ...form, month: e.target.value, quarter: '' })}>
                                    <option value="">—</option>
                                    {MONTHS.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
                                </select>
                            </div>
                            <div className="form-group">
                                <label className="erp-label">FECHA LÍMITE *</label>
                                <input className="erp-input" type="date" value={form.deadlineDate} onChange={e => setForm({ ...form, deadlineDate: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">FECHA RECORDATORIO *</label>
                                <input className="erp-input" type="date" value={form.reminderDate} onChange={e => setForm({ ...form, reminderDate: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">IMPORTE ESTIMADO</label>
                                <input className="erp-input" type="number" value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} placeholder="0.00" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">NOTAS</label>
                                <input className="erp-input" value={form.notes} onChange={e => setForm({ ...form, notes: e.target.value })} placeholder="Observaciones" />
                            </div>
                        </div>
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                            <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                            <button className="btn btn-primary" onClick={createEvent} disabled={saving}>{saving ? 'Guardando...' : '✓ Crear Evento'}</button>
                        </div>
                    </div>
                </div>
            )}
        </PageContainer>
    );
}
