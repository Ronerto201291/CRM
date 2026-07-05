'use client';
import { useState, useCallback } from 'react';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import { parseListResponse } from '@/lib/parseListResponse';
import { quoteHeaderSchema } from '@/lib/schemas/quoteHeaderSchema';
import { useCachedApi } from '@/hooks/useCachedApi';
import { updateLineAt } from '@/lib/lineForm';

interface QuoteSummary {
    id: string; number: string; seriesPrefix: string; fiscalYear: number; version: number;
    status: string; clientName?: string; clientType: string;
    issueDate: string; validUntil: string;
    totalAmount: number; taxAmount: number; taxBaseAmount: number;
    createdAt: string; convertedToInvoiceId?: string;
}
interface Client { id: string; name: string; taxId?: string; email?: string; }
interface Prospect { id: string; name: string; taxId: string; email: string; phone: string; address: string; status: string; }
interface LineForm { description: string; quantity: number; unitPrice: number; taxRate: number; discountPct: number; unit: string; }

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Draft:      { label: 'Borrador',   cls: 'badge-gray' },
    Sent:       { label: 'Enviado',    cls: 'badge-info' },
    Accepted:   { label: 'Aceptado',   cls: 'badge-success' },
    Rejected:   { label: 'Rechazado',  cls: 'badge-danger' },
    Expired:    { label: 'Expirado',   cls: 'badge-warning' },
    Converted:  { label: 'Convertido', cls: 'badge-purple' },
    Superseded: { label: 'Sustituido', cls: 'badge-gray' },
};

const emptyLine = (): LineForm => ({ description: '', quantity: 1, unitPrice: 0, taxRate: 21, discountPct: 0, unit: '' });

const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;
const fmtDate = (s: string) => new Date(s).toLocaleDateString('es-ES');
const today = () => new Date().toISOString().split('T')[0];
const plus30 = () => { const d = new Date(); d.setDate(d.getDate() + 30); return d.toISOString().split('T')[0]; };

export default function QuotesClient({
    initialQuotes,
    initialClients,
    initialProspects,
}: {
    initialQuotes: QuoteSummary[];
    initialClients: Client[];
    initialProspects: Prospect[];
}) {
    const [quotes, setQuotes] = useState<QuoteSummary[]>(initialQuotes);
    const [clients, setClients] = useState<Client[]>(initialClients);
    const [prospects, setProspects] = useState<Prospect[]>(initialProspects);
    const [loading, setLoading] = useState(false);
    const [filter, setFilter] = useState('all');

    // Create modal
    const [showCreate, setShowCreate] = useState(false);
    const [clientType, setClientType] = useState<'Registered' | 'Lead' | 'Manual'>('Registered');
    const [form, setForm] = useState({
        clientId: '', clientName: '', clientTaxId: '', clientEmail: '', clientPhone: '', clientAddress: '',
        issueDate: today(), validUntil: plus30(),
        globalDiscountPct: 0, notes: '', internalNotes: '',
        lines: [emptyLine()],
    });
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);
    const [sendError, setSendError] = useState<string | null>(null);
    const [actionError, setActionError] = useState<string | null>(null);
    const [successMsg, setSuccessMsg] = useState<string | null>(null);

    const [prospectSelected, setProspectSelected] = useState<Prospect | null>(null);

    // Send modal
    const [showSend, setShowSend] = useState(false);
    const [sendTarget, setSendTarget] = useState<QuoteSummary | null>(null);
    const [sendForm, setSendForm] = useState({ toEmail: '', toName: '', attachPdf: true });
    const [sending, setSending] = useState(false);

    // Reject modal
    const [showReject, setShowReject] = useState(false);
    const [rejectTarget, setRejectTarget] = useState<QuoteSummary | null>(null);
    const [rejectReason, setRejectReason] = useState('');

    // Convert modal
    const [showConvert, setShowConvert] = useState(false);
    const [convertTarget, setConvertTarget] = useState<QuoteSummary | null>(null);
    const [convertDueDate, setConvertDueDate] = useState(plus30());
    const { fetchCached, invalidateCached } = useCachedApi();

    const refresh = useCallback(async () => {
        setLoading(true);
        invalidateCached('quotes');
        invalidateCached('clients');
        invalidateCached('leads');
        const [qData, cData, lData] = await Promise.all([
            fetchCached<unknown>('quotes?pageSize=500'),
            fetchCached<unknown>('clients?pageSize=500'),
            fetchCached<unknown>('leads?pageSize=500'),
        ]);
        if (qData) setQuotes(parseListResponse<QuoteSummary>(qData));
        if (cData) setClients(parseListResponse<Client>(cData));
        if (lData) setProspects(parseListResponse<Prospect>(lData));
        setLoading(false);
    }, [fetchCached, invalidateCached]);

    // Datos iniciales vía RSC; refresh() tras mutaciones

    // ── Line helpers ──────────────────────────────────────────────────────────
    const updateLine = <K extends keyof LineForm>(i: number, field: K, val: LineForm[K]) =>
        setForm({ ...form, lines: updateLineAt(form.lines, i, field, val) });
    const addLine = () => setForm({ ...form, lines: [...form.lines, emptyLine()] });
    const removeLine = (i: number) => setForm({ ...form, lines: form.lines.filter((_, idx) => idx !== i) });

    // ── Totals ────────────────────────────────────────────────────────────────
    const round2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;
    const subtotalBefore = form.lines.reduce((s, l) => s + round2(l.quantity * l.unitPrice * (1 - l.discountPct / 100)), 0);
    const discAmt = round2(subtotalBefore * (form.globalDiscountPct / 100));
    const taxBase = round2(subtotalBefore - discAmt);
    const taxAmt = form.lines.reduce((s, l) => {
        const base = round2(round2(l.quantity * l.unitPrice * (1 - l.discountPct / 100)) * (1 - form.globalDiscountPct / 100));
        return s + round2(base * (l.taxRate / 100));
    }, 0);
    const totalAmt = round2(taxBase + taxAmt);

    // ── Actions ───────────────────────────────────────────────────────────────
    const handleCreate = async () => {
        setFormError(null);
        const headerCheck = quoteHeaderSchema.safeParse({
            clientType,
            clientId: form.clientId,
            clientName: form.clientName,
            clientTaxId: form.clientTaxId,
            clientEmail: form.clientEmail,
            clientPhone: form.clientPhone,
            clientAddress: form.clientAddress,
            issueDate: form.issueDate,
            validUntil: form.validUntil,
            globalDiscountPct: form.globalDiscountPct,
            notes: form.notes,
            internalNotes: form.internalNotes,
        });
        if (!headerCheck.success) {
            setFormError(headerCheck.error.issues[0]?.message ?? 'Datos inválidos');
            return;
        }
        if (form.lines.every(l => !l.description)) { setFormError('Añade al menos una línea'); return; }
        setSaving(true);
        try {
            const body = {
                clientType,
                clientId: clientType === 'Registered' || clientType === 'Lead' ? form.clientId : undefined,
                clientName: clientType === 'Manual' ? form.clientName : undefined,
                clientTaxId: form.clientTaxId || undefined,
                clientEmail: form.clientEmail || undefined,
                clientPhone: form.clientPhone || undefined,
                clientAddress: form.clientAddress || undefined,
                issueDate: new Date(form.issueDate).toISOString(),
                validUntil: new Date(form.validUntil).toISOString(),
                globalDiscountPct: form.globalDiscountPct,
                notes: form.notes || undefined,
                internalNotes: form.internalNotes || undefined,
                lines: form.lines.filter(l => l.description).map((l, i) => ({
                    description: l.description, quantity: l.quantity, unitPrice: l.unitPrice,
                    taxRate: l.taxRate, discountPct: l.discountPct, unit: l.unit || undefined, sortOrder: i,
                })),
            };
            const res = await fetch('/api/proxy/quotes', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body),
            });
            if (res.ok) {
                setShowCreate(false);
                setForm({ clientId: '', clientName: '', clientTaxId: '', clientEmail: '', clientPhone: '', clientAddress: '', issueDate: today(), validUntil: plus30(), globalDiscountPct: 0, notes: '', internalNotes: '', lines: [emptyLine()] });
                setClientType('Registered');
                setProspectSelected(null);
                refresh();
            } else {
                const e = await res.json();
                setFormError(e.error || 'Error al crear presupuesto');
            }
        } finally { setSaving(false); }
    };

    const openSend = (q: QuoteSummary) => {
        setSendTarget(q);
        setSendForm({ toEmail: '', toName: q.clientName || '', attachPdf: true });
        setShowSend(true);
    };

    const handleSend = async () => {
        setSendError(null);
        if (!sendForm.toEmail) { setSendError('Introduce el email'); return; }
        setSending(true);
        try {
            const res = await fetch(`/api/proxy/quotes/${sendTarget!.id}/send`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(sendForm),
            });
            if (res.ok) { setShowSend(false); refresh(); }
            else { const e = await res.json(); setSendError(e.error || 'Error al enviar'); }
        } finally { setSending(false); }
    };

    const handleAccept = async (id: string) => {
        if (!confirm('¿Marcar este presupuesto como ACEPTADO manualmente?')) return;
        setActionError(null);
        const res = await fetch(`/api/proxy/quotes/${id}/accept`, { method: 'POST' });
        if (res.ok) refresh();
        else { const e = await res.json(); setActionError(e.error || 'Error'); }
    };

    const openReject = (q: QuoteSummary) => { setRejectTarget(q); setRejectReason(''); setShowReject(true); };
    const handleReject = async () => {
        setActionError(null);
        const res = await fetch(`/api/proxy/quotes/${rejectTarget!.id}/reject`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ reason: rejectReason }),
        });
        if (res.ok) { setShowReject(false); refresh(); }
        else { const e = await res.json(); setActionError(e.error || 'Error'); }
    };

    const openConvert = (q: QuoteSummary) => { setConvertTarget(q); setConvertDueDate(plus30()); setShowConvert(true); };
    const handleConvert = async () => {
        setActionError(null);
        setSuccessMsg(null);
        const res = await fetch(`/api/proxy/quotes/${convertTarget!.id}/convert`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ dueDate: new Date(convertDueDate).toISOString() }),
        });
        if (res.ok) { setShowConvert(false); refresh(); setSuccessMsg('Presupuesto convertido a factura'); }
        else { const e = await res.json(); setActionError(e.error || 'Error al convertir'); }
    };

    const handleDuplicate = async (id: string) => {
        setActionError(null);
        const res = await fetch(`/api/proxy/quotes/${id}/duplicate`, { method: 'POST' });
        if (res.ok) refresh();
        else { const e = await res.json(); setActionError(e.error || 'Error'); }
    };

    const downloadPdf = (id: string, number: string) => {
        const a = document.createElement('a');
        a.href = `/api/proxy/quotes/${id}/pdf`;
        a.download = `Presupuesto_${number.replace('/', '-')}.pdf`;
        a.click();
    };

    // ── Filtered + KPIs ───────────────────────────────────────────────────────
    const filtered = filter === 'all' ? quotes : quotes.filter(q => q.status === filter);
    const kpis = {
        total: quotes.reduce((s, q) => s + q.totalAmount, 0),
        pending: quotes.filter(q => q.status === 'Sent').length,
        accepted: quotes.filter(q => q.status === 'Accepted' || q.status === 'Converted').reduce((s, q) => s + q.totalAmount, 0),
        expired: quotes.filter(q => q.status === 'Expired').length,
    };

    const isExpiringSoon = (validUntil: string) => {
        const diff = new Date(validUntil).getTime() - Date.now();
        return diff > 0 && diff < 7 * 86400000;
    };

    return (
        <PageContainer>
            {/* Header */}
            <div className="page-header">
                <div>
                    <h1 className="page-title">Presupuestos</h1>
                    <p className="page-subtitle">Crea y gestiona presupuestos · Portal de aceptación para clientes</p>
                </div>
                <button className="btn btn-primary" onClick={() => setShowCreate(true)}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}><path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" /></svg>
                    Nuevo Presupuesto
                </button>
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

            {/* Filter tabs */}
            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px', flexWrap: 'wrap' }}>
                {['all', 'Draft', 'Sent', 'Accepted', 'Rejected', 'Expired', 'Converted'].map(f => (
                    <button key={f} onClick={() => setFilter(f)} style={{
                        padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)',
                        fontSize: '12px', fontWeight: filter === f ? 700 : 500,
                        background: filter === f ? 'var(--brand-primary)' : 'var(--surface)',
                        color: filter === f ? 'white' : 'var(--text-secondary)', cursor: 'pointer', transition: 'all 0.15s',
                    }}>
                        {f === 'all' ? 'Todos' : STATUS_MAP[f]?.label ?? f}
                        {f !== 'all' && (
                            <span style={{ marginLeft: '6px', fontSize: '11px', opacity: 0.7 }}>
                                ({quotes.filter(q => q.status === f).length})
                            </span>
                        )}
                    </button>
                ))}
            </div>

            {/* KPIs */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '14px', marginBottom: '20px' }}>
                <MiniStat label="Volumen Total" value={fmt(kpis.total)} color="var(--brand-primary)" />
                <MiniStat label="Pendientes Respuesta" value={String(kpis.pending)} color="var(--warning)" />
                <MiniStat label="Aceptados / Convertidos" value={fmt(kpis.accepted)} color="var(--success)" />
                <MiniStat label="Expirados" value={String(kpis.expired)} color="var(--danger)" />
            </div>

            {/* Table */}
            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Cliente</th>
                            <th>Emisión</th>
                            <th>Válido hasta</th>
                            <th style={{ textAlign: 'right' }}>Base Imp.</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                            <th>Estado</th>
                            <th style={{ textAlign: 'right' }}>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>
                        {loading && (
                            <tr><td colSpan={8} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>Cargando...</td></tr>
                        )}
                        {!loading && filtered.length === 0 && (
                            <tr><td colSpan={8}>
                                <div className="empty-state">
                                    <div className="empty-state-icon">📋</div>
                                    <div className="empty-state-title">Sin presupuestos</div>
                                    <div className="empty-state-sub">Crea tu primer presupuesto con el botón superior</div>
                                </div>
                            </td></tr>
                        )}
                        {filtered.map(q => (
                            <tr key={q.id}>
                                <td>
                                    <a href={`/billing/quotes/${q.id}`} style={{ fontWeight: 700, fontFamily: 'monospace', fontSize: '12px', color: 'var(--brand-primary)', textDecoration: 'none' }}>
                                        {q.number}
                                    </a>
                                    {q.version > 1 && <span style={{ marginLeft: '5px', fontSize: '10px', color: 'var(--text-muted)' }}>v{q.version}</span>}
                                </td>
                                <td style={{ color: 'var(--text-secondary)' }}>{q.clientName || <span style={{ color: 'var(--text-muted)' }}>—</span>}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{fmtDate(q.issueDate)}</td>
                                <td style={{ color: isExpiringSoon(q.validUntil) ? 'var(--warning)' : (new Date(q.validUntil) < new Date() && q.status === 'Sent' ? 'var(--danger)' : 'var(--text-secondary)') }}>
                                    {fmtDate(q.validUntil)}
                                    {isExpiringSoon(q.validUntil) && q.status === 'Sent' && <span style={{ marginLeft: '5px', fontSize: '10px' }}>⚠️</span>}
                                </td>
                                <td style={{ textAlign: 'right', color: 'var(--text-secondary)' }}>{fmt(q.taxBaseAmount)}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(q.totalAmount)}</td>
                                <td><span className={`badge ${STATUS_MAP[q.status]?.cls ?? 'badge-gray'}`}>{STATUS_MAP[q.status]?.label ?? q.status}</span></td>
                                <td style={{ textAlign: 'right' }}>
                                    <div style={{ display: 'flex', gap: '5px', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                                        {q.status === 'Draft' && (
                                            <button className="btn btn-primary btn-sm" onClick={() => openSend(q)}>Enviar</button>
                                        )}
                                        {q.status === 'Sent' && (<>
                                            <button className="btn btn-success btn-sm" onClick={() => handleAccept(q.id)}>Aceptar</button>
                                            <button className="btn btn-sm" style={{ background: 'var(--danger-bg)', color: 'var(--danger)' }} onClick={() => openReject(q)}>Rechazar</button>
                                        </>)}
                                        {q.status === 'Accepted' && (
                                            <button className="btn btn-primary btn-sm" onClick={() => openConvert(q)}>→ Factura</button>
                                        )}
                                        {q.convertedToInvoiceId && (
                                            <a href={`/billing/${q.convertedToInvoiceId}`} className="btn btn-secondary btn-sm" style={{ textDecoration: 'none' }}>Ver factura</a>
                                        )}
                                        <button className="btn btn-secondary btn-sm" onClick={() => downloadPdf(q.id, q.number)} title="Descargar PDF">PDF</button>
                                        <button className="btn btn-secondary btn-sm" onClick={() => handleDuplicate(q.id)} title="Duplicar">⧉</button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {/* ─── Create Modal ─────────────────────────────────────────────────── */}
            <AccessibleModal open={showCreate} onClose={() => setShowCreate(false)} title="Nuevo Presupuesto" maxWidth="780px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowCreate(false)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={handleCreate} disabled={saving}>{saving ? 'Creando...' : '✓ Crear Presupuesto'}</button>
                </div>)}>

                        {formError && (
                            <div style={{ padding: '10px 12px', marginBottom: 14, borderRadius: 8, color: 'var(--danger)', background: 'var(--danger-bg)', fontSize: 13 }}>
                                {formError}
                            </div>
                        )}

                        {/* Client type toggle */}
                        <div style={{ marginBottom: '16px' }}>
                            <label className="erp-label">TIPO DE CLIENTE</label>
                            <div style={{ display: 'flex', gap: '8px', marginTop: '6px', flexWrap: 'wrap' }}>
                                {([
                                    { value: 'Registered', label: '📋 Cliente registrado' },
                                    { value: 'Lead',       label: '🔍 Posible cliente' },
                                    { value: 'Manual',     label: '✏️ Cliente manual' },
                                ] as const).map(t => (
                                    <button key={t.value} onClick={() => {
                                        setClientType(t.value);
                                        setProspectSelected(null);
                                        setForm(f => ({ ...f, clientId: '', clientName: '', clientTaxId: '', clientEmail: '', clientPhone: '', clientAddress: '' }));
                                    }} style={{
                                        padding: '6px 16px', borderRadius: '6px', border: '1px solid var(--border)',
                                        fontSize: '12px', fontWeight: 600, cursor: 'pointer', transition: 'all 0.15s',
                                        background: clientType === t.value ? 'var(--brand-primary)' : 'var(--surface)',
                                        color: clientType === t.value ? 'white' : 'var(--text-secondary)',
                                    }}>
                                        {t.label}
                                    </button>
                                ))}
                            </div>
                        </div>

                        {/* Client fields */}
                        {clientType === 'Registered' && (
                            <div className="form-group" style={{ marginBottom: '16px' }}>
                                <label className="erp-label">CLIENTE *</label>
                                <select className="erp-input" value={form.clientId} onChange={e => setForm({ ...form, clientId: e.target.value })}>
                                    <option value="">Seleccionar cliente...</option>
                                    {clients.map(c => <option key={c.id} value={c.id}>{c.name}{c.taxId ? ` — ${c.taxId}` : ''}</option>)}
                                </select>
                            </div>
                        )}

                        {clientType === 'Lead' && (
                            <div style={{ marginBottom: '16px' }}>
                                <div className="form-group" style={{ marginBottom: '10px' }}>
                                    <label className="erp-label">POSIBLE CLIENTE *</label>
                                    <select className="erp-input" value={form.clientId} onChange={e => {
                                        const p = prospects.find(x => x.id === e.target.value) ?? null;
                                        setProspectSelected(p);
                                        setForm(f => ({
                                            ...f,
                                            clientId:      p?.id      ?? '',
                                            clientTaxId:   p?.taxId   ?? '',
                                            clientEmail:   p?.email   ?? '',
                                            clientPhone:   p?.phone   ?? '',
                                            clientAddress: p?.address ?? '',
                                        }));
                                    }}>
                                        <option value="">Seleccionar posible cliente...</option>
                                        {prospects.filter(p => !['Won', 'Lost'].includes(p.status)).map(p => (
                                            <option key={p.id} value={p.id}>{p.name}{p.taxId ? ` — ${p.taxId}` : ''}</option>
                                        ))}
                                    </select>
                                </div>
                                {prospectSelected && (
                                    <div style={{ background: 'var(--surface-2)', borderRadius: '8px', padding: '10px 14px', fontSize: '12px', color: 'var(--text-secondary)', display: 'flex', gap: '16px', flexWrap: 'wrap' }}>
                                        {prospectSelected.email && <span>✉ {prospectSelected.email}</span>}
                                        {prospectSelected.phone && <span>📞 {prospectSelected.phone}</span>}
                                        {prospectSelected.taxId && <span>NIF: {prospectSelected.taxId}</span>}
                                        {prospectSelected.address && <span>📍 {prospectSelected.address}</span>}
                                    </div>
                                )}
                            </div>
                        )}

                        {clientType === 'Manual' && (
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                                <div className="form-group">
                                    <label className="erp-label">NOMBRE / RAZÓN SOCIAL *</label>
                                    <input className="erp-input" value={form.clientName} onChange={e => setForm({ ...form, clientName: e.target.value })} placeholder="Nombre del cliente" />
                                </div>
                                <div className="form-group">
                                    <label className="erp-label">NIF / CIF</label>
                                    <input className="erp-input" value={form.clientTaxId} onChange={e => setForm({ ...form, clientTaxId: e.target.value })} placeholder="B12345678" />
                                </div>
                                <div className="form-group">
                                    <label className="erp-label">EMAIL</label>
                                    <input type="email" className="erp-input" value={form.clientEmail} onChange={e => setForm({ ...form, clientEmail: e.target.value })} placeholder="cliente@empresa.com" />
                                </div>
                                <div className="form-group">
                                    <label className="erp-label">TELÉFONO</label>
                                    <input className="erp-input" value={form.clientPhone} onChange={e => setForm({ ...form, clientPhone: e.target.value })} placeholder="+34 600 000 000" />
                                </div>
                                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                                    <label className="erp-label">DIRECCIÓN</label>
                                    <input className="erp-input" value={form.clientAddress} onChange={e => setForm({ ...form, clientAddress: e.target.value })} placeholder="Calle, número, ciudad" />
                                </div>
                            </div>
                        )}

                        {/* Dates + discount */}
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                            <div className="form-group">
                                <label className="erp-label">FECHA EMISIÓN</label>
                                <input type="date" className="erp-input" value={form.issueDate} onChange={e => setForm({ ...form, issueDate: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">VÁLIDO HASTA</label>
                                <input type="date" className="erp-input" value={form.validUntil} onChange={e => setForm({ ...form, validUntil: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">DESCUENTO GLOBAL (%)</label>
                                <input type="number" className="erp-input" value={form.globalDiscountPct} min={0} max={100} step={0.5}
                                    onChange={e => setForm({ ...form, globalDiscountPct: +e.target.value })} />
                            </div>
                        </div>

                        {/* Lines */}
                        <div style={{ marginBottom: '16px' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                                <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Líneas</label>
                                <button className="btn btn-secondary btn-sm" onClick={addLine}>+ Añadir línea</button>
                            </div>
                            <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                                    <thead style={{ background: 'var(--surface-2)' }}>
                                        <tr>
                                            {['DESCRIPCIÓN', 'CANT.', 'P.UNIT.', 'DTO%', 'IVA%', 'TOTAL', ''].map((h, i) => (
                                                <th key={i} style={{ padding: '8px 8px', textAlign: i >= 1 && i <= 5 ? 'right' : 'left', fontSize: '10px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: i === 0 ? 'auto' : i === 6 ? '28px' : '75px' }}>{h}</th>
                                            ))}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {form.lines.map((line, i) => (
                                            <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                                                <td style={{ padding: '5px 8px' }}>
                                                    <input className="erp-input" style={{ fontSize: '12px', padding: '4px 7px' }}
                                                        placeholder="Descripción del servicio o producto"
                                                        value={line.description} onChange={e => updateLine(i, 'description', e.target.value)} />
                                                </td>
                                                <td style={{ padding: '5px 4px' }}>
                                                    <input type="number" className="erp-input" style={{ fontSize: '12px', padding: '4px 6px', textAlign: 'right' }}
                                                        value={line.quantity} min={0.001} step={0.001} onChange={e => updateLine(i, 'quantity', +e.target.value)} />
                                                </td>
                                                <td style={{ padding: '5px 4px' }}>
                                                    <input type="number" className="erp-input" style={{ fontSize: '12px', padding: '4px 6px', textAlign: 'right' }}
                                                        value={line.unitPrice} min={0} step={0.01} onChange={e => updateLine(i, 'unitPrice', +e.target.value)} />
                                                </td>
                                                <td style={{ padding: '5px 4px' }}>
                                                    <input type="number" className="erp-input" style={{ fontSize: '12px', padding: '4px 6px', textAlign: 'right' }}
                                                        value={line.discountPct} min={0} max={100} step={0.5} onChange={e => updateLine(i, 'discountPct', +e.target.value)} />
                                                </td>
                                                <td style={{ padding: '5px 4px' }}>
                                                    <select className="erp-input" style={{ fontSize: '12px', padding: '4px 6px' }}
                                                        value={line.taxRate} onChange={e => updateLine(i, 'taxRate', +e.target.value)}>
                                                        <option value={21}>21%</option>
                                                        <option value={10}>10%</option>
                                                        <option value={4}>4%</option>
                                                        <option value={0}>0%</option>
                                                    </select>
                                                </td>
                                                <td style={{ padding: '5px 4px', textAlign: 'right', fontSize: '12px', fontWeight: 600 }}>
                                                    {fmt(round2(round2(line.quantity * line.unitPrice * (1 - line.discountPct / 100)) * (1 + line.taxRate / 100)))}
                                                </td>
                                                <td style={{ padding: '5px 2px', textAlign: 'center' }}>
                                                    {form.lines.length > 1 && (
                                                        <button onClick={() => removeLine(i)} style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--danger)', fontSize: '15px' }}>✕</button>
                                                    )}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </div>

                        {/* Totals summary */}
                        <div style={{ background: 'var(--surface-2)', borderRadius: '8px', padding: '12px 16px', marginBottom: '16px' }}>
                            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '5px', minWidth: '220px', fontSize: '13px' }}>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span style={{ color: 'var(--text-secondary)' }}>Subtotal bruto</span><span>{fmt(subtotalBefore + discAmt)}</span></div>
                                    {form.globalDiscountPct > 0 && <div style={{ display: 'flex', justifyContent: 'space-between' }}><span style={{ color: 'var(--danger)' }}>Dto. global ({form.globalDiscountPct}%)</span><span style={{ color: 'var(--danger)' }}>– {fmt(discAmt)}</span></div>}
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span style={{ color: 'var(--text-secondary)' }}>Base imponible</span><span>{fmt(taxBase)}</span></div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span style={{ color: 'var(--text-secondary)' }}>IVA</span><span>{fmt(taxAmt)}</span></div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid var(--border)', paddingTop: '6px', marginTop: '2px' }}>
                                        <span style={{ fontWeight: 800, fontSize: '15px' }}>Total</span>
                                        <span style={{ fontWeight: 800, fontSize: '15px', color: 'var(--brand-primary)' }}>{fmt(totalAmt)}</span>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Notes */}
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group">
                                <label className="erp-label">NOTAS AL CLIENTE</label>
                                <textarea className="erp-input" rows={3} value={form.notes} onChange={e => setForm({ ...form, notes: e.target.value })} placeholder="Condiciones, plazos de pago, etc." style={{ resize: 'vertical' }} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">NOTAS INTERNAS</label>
                                <textarea className="erp-input" rows={3} value={form.internalNotes} onChange={e => setForm({ ...form, internalNotes: e.target.value })} placeholder="No visibles para el cliente" style={{ resize: 'vertical' }} />
                            </div>
                        </div>
            </AccessibleModal>

            <AccessibleModal open={!!showSend} onClose={() => setShowSend(false)} title="Enviar Presupuesto" maxWidth="440px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowSend(false)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={handleSend} disabled={sending}>{sending ? 'Enviando...' : '📧 Enviar'}</button>
                </div>)}>
                        <div style={{ background: 'var(--surface-2)', borderRadius: '8px', padding: '10px 14px', marginBottom: '16px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                            Presupuesto <strong>{sendTarget?.number}</strong> · Se enviará un link de aceptación al cliente
                        </div>
                        {sendError && (
                            <div style={{ background: 'var(--danger-bg)', border: '1px solid rgba(239,68,68,0.25)', borderRadius: '8px', padding: '10px 14px', marginBottom: '16px', fontSize: '13px', color: 'var(--danger)' }}>
                                {sendError}
                            </div>
                        )}
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group">
                                <label className="erp-label">EMAIL DESTINATARIO *</label>
                                <input type="email" className="erp-input" value={sendForm.toEmail}
                                    onChange={e => setSendForm({ ...sendForm, toEmail: e.target.value })} placeholder="cliente@empresa.com" />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">NOMBRE DESTINATARIO</label>
                                <input className="erp-input" value={sendForm.toName}
                                    onChange={e => setSendForm({ ...sendForm, toName: e.target.value })} placeholder="Nombre del contacto" />
                            </div>
                            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', cursor: 'pointer' }}>
                                <input type="checkbox" checked={sendForm.attachPdf}
                                    onChange={e => setSendForm({ ...sendForm, attachPdf: e.target.checked })} />
                                Adjuntar PDF del presupuesto
                            </label>
                        </div>
            </AccessibleModal>

            <AccessibleModal open={!!showReject} onClose={() => setShowReject(false)} title="Rechazar Presupuesto" maxWidth="420px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowReject(false)}>Cancelar</button>
                    <button className="btn btn-sm" style={{ background: 'var(--danger-bg)', color: 'var(--danger)', padding: '8px 16px', borderRadius: '7px', border: 'none', cursor: 'pointer', fontWeight: 600 }}
                        onClick={handleReject}>Confirmar Rechazo</button>
                </div>)}>
                        <div className="form-group" style={{ marginBottom: '20px' }}>
                            <label className="erp-label">MOTIVO DEL RECHAZO</label>
                            <textarea className="erp-input" rows={3} value={rejectReason}
                                onChange={e => setRejectReason(e.target.value)} placeholder="Precio elevado, plazos, etc. (opcional)" style={{ resize: 'vertical' }} />
                        </div>
            </AccessibleModal>

            <AccessibleModal open={!!showConvert} onClose={() => setShowConvert(false)} title="Convertir a Factura" maxWidth="400px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowConvert(false)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={handleConvert}>✓ Convertir</button>
                </div>)}>
                        <div style={{ background: 'var(--surface-2)', borderRadius: '8px', padding: '10px 14px', marginBottom: '16px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                            El presupuesto <strong>{convertTarget?.number}</strong> ({fmt(convertTarget?.totalAmount ?? 0)}) se convertirá en factura. El cliente debe estar registrado.
                        </div>
                        <div className="form-group" style={{ marginBottom: '20px' }}>
                            <label className="erp-label">FECHA VENCIMIENTO FACTURA</label>
                            <input type="date" className="erp-input" value={convertDueDate} onChange={e => setConvertDueDate(e.target.value)} />
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
