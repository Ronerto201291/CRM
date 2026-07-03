'use client';
import { useState, useCallback } from 'react';
import AccessibleModal from '@/components/AccessibleModal';

interface QuoteLineDetail {
    id: string; sortOrder: number; description: string; productCode?: string; unit?: string;
    quantity: number; unitPrice: number; discountPct: number; discountAmount: number;
    taxRate: number; lineSubtotal: number; lineTaxBase: number; lineTaxAmount: number; lineTotalAmount: number;
}
interface TaxGroup { taxRate: number; baseAmount: number; taxAmount: number; }
interface StatusEntry { id: string; fromStatus?: string; toStatus: string; changedAt: string; reason?: string; }
interface QuoteDetail {
    id: string; number: string; seriesPrefix: string; fiscalYear: number; version: number;
    status: string; clientId?: string; clientName?: string; clientType: string;
    clientTaxId?: string; clientEmail?: string; clientPhone?: string; clientAddress?: string;
    issueDate: string; validUntil: string; sentAt?: string; acceptedAt?: string; rejectedAt?: string; convertedAt?: string;
    globalDiscountPct: number; globalDiscountAmount: number;
    subtotalBeforeDisc: number; subtotalAfterDisc: number;
    taxBaseAmount: number; taxAmount: number; totalAmount: number;
    notes?: string; internalNotes?: string; currency: string;
    convertedToInvoiceId?: string; createdAt: string;
    lines: QuoteLineDetail[];
    taxBreakdown: TaxGroup[];
    statusHistory: StatusEntry[];
}

const STATUS_MAP: Record<string, { label: string; color: string; bg: string }> = {
    Draft:      { label: 'Borrador',   color: '#6b7a8d', bg: '#f1f5f9' },
    Sent:       { label: 'Enviado',    color: '#0369a1', bg: '#e0f2fe' },
    Accepted:   { label: 'Aceptado',   color: '#15803d', bg: '#dcfce7' },
    Rejected:   { label: 'Rechazado',  color: '#b91c1c', bg: '#fee2e2' },
    Expired:    { label: 'Expirado',   color: '#b45309', bg: '#fef3c7' },
    Converted:  { label: 'Convertido', color: '#6d28d9', bg: '#ede9fe' },
    Superseded: { label: 'Sustituido', color: '#6b7a8d', bg: '#f1f5f9' },
};

const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;
const fmtDate = (s?: string) => s ? new Date(s).toLocaleDateString('es-ES') : '—';
const fmtDateTime = (s?: string) => s ? new Date(s).toLocaleString('es-ES') : '—';

interface QuoteDetailClientProps {
    id: string;
    initialQuote: QuoteDetail | null;
}

export default function QuoteDetailClient({ id, initialQuote }: QuoteDetailClientProps) {
    const [quote, setQuote] = useState<QuoteDetail | null>(initialQuote);
    const [loading, setLoading] = useState(false);

    // Modals
    const [showSend, setShowSend] = useState(false);
    const [sendForm, setSendForm] = useState({ toEmail: '', toName: '', attachPdf: true });
    const [sending, setSending] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);
    const [actionError, setActionError] = useState<string | null>(null);
    const [successMsg, setSuccessMsg] = useState<string | null>(null);
    const [showReject, setShowReject] = useState(false);
    const [rejectReason, setRejectReason] = useState('');
    const [showConvert, setShowConvert] = useState(false);
    const [convertDueDate, setConvertDueDate] = useState(() => {
        const d = new Date(); d.setDate(d.getDate() + 30); return d.toISOString().split('T')[0];
    });

    const load = useCallback(async () => {
        setLoading(true);
        const r = await fetch(`/api/proxy/quotes/${id}`);
        if (r.ok) setQuote(await r.json());
        setLoading(false);
    }, [id]);

    // ── Actions ───────────────────────────────────────────────────────────────
    const handleSend = async () => {
        setFormError(null);
        if (!sendForm.toEmail) { setFormError('Introduce el email'); return; }
        setSending(true);
        try {
            const res = await fetch(`/api/proxy/quotes/${id}/send`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(sendForm),
            });
            if (res.ok) { setShowSend(false); setSuccessMsg('Presupuesto enviado'); load(); }
            else { const e = await res.json(); setFormError(e.error || 'Error al enviar'); }
        } finally { setSending(false); }
    };

    const handleAccept = async () => {
        if (!confirm('¿Marcar como ACEPTADO manualmente?')) return;
        setActionError(null);
        setSuccessMsg(null);
        const res = await fetch(`/api/proxy/quotes/${id}/accept`, { method: 'POST' });
        if (res.ok) { setSuccessMsg('Presupuesto aceptado'); load(); }
        else { const e = await res.json(); setActionError(e.error || 'Error'); }
    };

    const handleReject = async () => {
        setActionError(null);
        const res = await fetch(`/api/proxy/quotes/${id}/reject`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ reason: rejectReason }),
        });
        if (res.ok) { setShowReject(false); setSuccessMsg('Presupuesto rechazado'); load(); }
        else { const e = await res.json(); setActionError(e.error || 'Error'); }
    };

    const handleConvert = async () => {
        setActionError(null);
        setSuccessMsg(null);
        const res = await fetch(`/api/proxy/quotes/${id}/convert`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ dueDate: new Date(convertDueDate).toISOString() }),
        });
        if (res.ok) {
            setShowConvert(false);
            const data = await res.json();
            setSuccessMsg('Convertido a factura');
            load();
            if (data?.invoiceId) window.location.href = `/billing/${data.invoiceId}`;
        } else { const e = await res.json(); setActionError(e.error || 'Error al convertir'); }
    };

    const handleDuplicate = async () => {
        setActionError(null);
        const res = await fetch(`/api/proxy/quotes/${id}/duplicate`, { method: 'POST' });
        if (res.ok) {
            const newId = await res.json();
            window.location.href = `/billing/quotes/${newId}`;
        } else { const e = await res.json(); setActionError(e.error || 'Error'); }
    };

    const downloadPdf = () => {
        const a = document.createElement('a');
        a.href = `/api/proxy/quotes/${id}/pdf`;
        a.download = `Presupuesto_${quote?.number.replace('/', '-')}.pdf`;
        a.click();
    };

    // ── Render ────────────────────────────────────────────────────────────────
    if (loading) return (
        <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)', fontFamily: 'Inter, sans-serif' }}>Cargando presupuesto...</div>
    );
    if (!quote) return (
        <div style={{ padding: '60px', textAlign: 'center', color: 'var(--danger)', fontFamily: 'Inter, sans-serif' }}>Presupuesto no encontrado</div>
    );

    const statusInfo = STATUS_MAP[quote.status] ?? { label: quote.status, color: '#6b7a8d', bg: '#f1f5f9' };

    return (
        <div style={{ padding: '28px 32px', fontFamily: 'Inter, sans-serif' }}>
            {/* Header */}
            <div className="page-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                    <a href="/billing/quotes" style={{ color: 'var(--text-muted)', textDecoration: 'none', fontSize: '13px' }}>← Presupuestos</a>
                    <div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                            <h1 style={{ fontSize: '22px', fontWeight: 800, fontFamily: 'monospace' }}>{quote.number}</h1>
                            {quote.version > 1 && <span style={{ fontSize: '12px', color: 'var(--text-muted)', background: 'var(--surface-2)', padding: '2px 8px', borderRadius: '4px' }}>v{quote.version}</span>}
                            <span style={{ padding: '4px 12px', borderRadius: '6px', fontSize: '12px', fontWeight: 700, background: statusInfo.bg, color: statusInfo.color }}>{statusInfo.label}</span>
                        </div>
                        <p style={{ margin: '2px 0 0', fontSize: '13px', color: 'var(--text-muted)' }}>
                            Emitido el {fmtDate(quote.issueDate)} · Válido hasta {fmtDate(quote.validUntil)}
                        </p>
                    </div>
                </div>
                <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                    {quote.status === 'Draft' && (
                        <button className="btn btn-primary" onClick={() => { setSendForm({ toEmail: quote.clientEmail || '', toName: quote.clientName || '', attachPdf: true }); setShowSend(true); }}>
                            📧 Enviar al cliente
                        </button>
                    )}
                    {quote.status === 'Sent' && (<>
                        <button className="btn btn-success" onClick={handleAccept}>✓ Aceptar</button>
                        <button className="btn btn-secondary" onClick={() => { setRejectReason(''); setShowReject(true); }}>✕ Rechazar</button>
                    </>)}
                    {quote.status === 'Accepted' && (
                        <button className="btn btn-primary" onClick={() => setShowConvert(true)}>→ Convertir a Factura</button>
                    )}
                    {quote.convertedToInvoiceId && (
                        <a href={`/billing/${quote.convertedToInvoiceId}`} className="btn btn-secondary" style={{ textDecoration: 'none' }}>Ver Factura →</a>
                    )}
                    <button className="btn btn-secondary" onClick={downloadPdf}>⬇ PDF</button>
                    <button className="btn btn-secondary" onClick={handleDuplicate}>⧉ Duplicar</button>
                </div>
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

            {/* Main content grid */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginBottom: '20px' }}>
                {/* Client info */}
                <div className="erp-card" style={{ padding: '18px 20px' }}>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '12px' }}>Datos del Cliente</div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', fontSize: '13px' }}>
                        <div style={{ fontWeight: 700, fontSize: '15px' }}>{quote.clientName || <span style={{ color: 'var(--text-muted)' }}>Sin nombre</span>}</div>
                        {quote.clientTaxId && <div style={{ color: 'var(--text-secondary)' }}>NIF/CIF: <strong>{quote.clientTaxId}</strong></div>}
                        {quote.clientEmail && <div style={{ color: 'var(--text-secondary)' }}>📧 {quote.clientEmail}</div>}
                        {quote.clientPhone && <div style={{ color: 'var(--text-secondary)' }}>📞 {quote.clientPhone}</div>}
                        {quote.clientAddress && <div style={{ color: 'var(--text-secondary)' }}>📍 {quote.clientAddress}</div>}
                        <div style={{ marginTop: '4px', fontSize: '11px', color: 'var(--text-muted)', background: 'var(--surface-2)', padding: '4px 8px', borderRadius: '4px', display: 'inline-block' }}>
                            Tipo: {quote.clientType}
                        </div>
                    </div>
                </div>

                {/* Quote metadata */}
                <div className="erp-card" style={{ padding: '18px 20px' }}>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '12px' }}>Datos del Presupuesto</div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', fontSize: '13px' }}>
                        {[
                            ['Serie / Nº', `${quote.seriesPrefix} · ${quote.number}`],
                            ['Ejercicio', String(quote.fiscalYear)],
                            ['Fecha emisión', fmtDate(quote.issueDate)],
                            ['Válido hasta', fmtDate(quote.validUntil)],
                            ['Enviado', fmtDate(quote.sentAt)],
                            ['Aceptado', fmtDate(quote.acceptedAt)],
                            ['Rechazado', fmtDate(quote.rejectedAt)],
                            ['Convertido', fmtDate(quote.convertedAt)],
                        ].filter(([, v]) => v !== '—').map(([k, v]) => (
                            <div key={k}>
                                <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '2px' }}>{k}</div>
                                <div style={{ fontWeight: 600 }}>{v}</div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            {/* Lines table */}
            <div className="erp-card" style={{ overflow: 'hidden', marginBottom: '20px' }}>
                <div style={{ padding: '14px 18px', borderBottom: '1px solid var(--border)', fontSize: '13px', fontWeight: 700 }}>Líneas del Presupuesto</div>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Descripción</th>
                            <th>Cód.</th>
                            <th>Ud.</th>
                            <th style={{ textAlign: 'right' }}>Cant.</th>
                            <th style={{ textAlign: 'right' }}>P. Unit.</th>
                            <th style={{ textAlign: 'right' }}>Dto %</th>
                            <th style={{ textAlign: 'right' }}>Base imp.</th>
                            <th style={{ textAlign: 'right' }}>IVA %</th>
                            <th style={{ textAlign: 'right' }}>Total línea</th>
                        </tr>
                    </thead>
                    <tbody>
                        {[...quote.lines].sort((a, b) => a.sortOrder - b.sortOrder).map(line => (
                            <tr key={line.id}>
                                <td style={{ fontWeight: 500 }}>{line.description}</td>
                                <td style={{ color: 'var(--text-muted)', fontSize: '11px', fontFamily: 'monospace' }}>{line.productCode || '—'}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{line.unit || 'ud'}</td>
                                <td style={{ textAlign: 'right' }}>{line.quantity.toLocaleString('es-ES', { maximumFractionDigits: 4 })}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(line.unitPrice)}</td>
                                <td style={{ textAlign: 'right', color: line.discountPct > 0 ? 'var(--danger)' : 'var(--text-muted)' }}>
                                    {line.discountPct > 0 ? `${line.discountPct}%` : '—'}
                                </td>
                                <td style={{ textAlign: 'right' }}>{fmt(line.lineTaxBase)}</td>
                                <td style={{ textAlign: 'right', color: 'var(--text-secondary)' }}>{line.taxRate}%</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(line.lineTotalAmount)}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {/* Bottom: tax breakdown + totals + history */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '20px', marginBottom: '20px' }}>
                {/* Tax breakdown */}
                <div className="erp-card" style={{ padding: '18px 20px' }}>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '12px' }}>Desglose Fiscal</div>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                        <thead>
                            <tr style={{ borderBottom: '1px solid var(--border)' }}>
                                {['Tipo IVA', 'Base imp.', 'Cuota'].map(h => (
                                    <th key={h} style={{ padding: '5px 0', textAlign: h === 'Tipo IVA' ? 'left' : 'right', fontSize: '10px', color: 'var(--text-muted)', fontWeight: 700, textTransform: 'uppercase' }}>{h}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {quote.taxBreakdown.map(g => (
                                <tr key={g.taxRate} style={{ borderBottom: '1px solid var(--border)' }}>
                                    <td style={{ padding: '6px 0' }}>{g.taxRate}%</td>
                                    <td style={{ textAlign: 'right', padding: '6px 0' }}>{fmt(g.baseAmount)}</td>
                                    <td style={{ textAlign: 'right', padding: '6px 0' }}>{fmt(g.taxAmount)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>

                {/* Totals */}
                <div className="erp-card" style={{ padding: '18px 20px' }}>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '12px' }}>Importes</div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', fontSize: '13px' }}>
                        <TotalRow label="Subtotal bruto" value={fmt(quote.subtotalBeforeDisc)} />
                        {quote.globalDiscountPct > 0 && (
                            <TotalRow label={`Descuento global (${quote.globalDiscountPct}%)`} value={`– ${fmt(quote.globalDiscountAmount)}`} valueColor="var(--danger)" />
                        )}
                        <TotalRow label="Base imponible" value={fmt(quote.taxBaseAmount)} />
                        <TotalRow label="Total IVA" value={fmt(quote.taxAmount)} />
                        <div style={{ borderTop: '2px solid var(--border)', paddingTop: '10px', marginTop: '4px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <span style={{ fontWeight: 800, fontSize: '15px' }}>TOTAL</span>
                            <span style={{ fontWeight: 800, fontSize: '17px', color: 'var(--brand-primary)' }}>{fmt(quote.totalAmount)}</span>
                        </div>
                    </div>
                </div>

                {/* Notes */}
                <div className="erp-card" style={{ padding: '18px 20px' }}>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '12px' }}>Notas</div>
                    {quote.notes ? (
                        <div style={{ fontSize: '13px', color: 'var(--text-secondary)', lineHeight: 1.6, marginBottom: '12px' }}>{quote.notes}</div>
                    ) : (
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontStyle: 'italic' }}>Sin notas al cliente</div>
                    )}
                    {quote.internalNotes && (
                        <div style={{ marginTop: '12px', borderTop: '1px solid var(--border)', paddingTop: '10px' }}>
                            <div style={{ fontSize: '10px', color: 'var(--text-muted)', fontWeight: 700, marginBottom: '4px' }}>NOTAS INTERNAS</div>
                            <div style={{ fontSize: '12px', color: 'var(--text-secondary)', background: '#fef9c3', padding: '8px 10px', borderRadius: '6px' }}>{quote.internalNotes}</div>
                        </div>
                    )}
                </div>
            </div>

            {/* Status history */}
            {quote.statusHistory.length > 0 && (
                <div className="erp-card" style={{ padding: '18px 20px' }}>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '14px' }}>Historial de Estado</div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                        {[...quote.statusHistory].sort((a, b) => new Date(a.changedAt).getTime() - new Date(b.changedAt).getTime()).map(h => {
                            const toInfo = STATUS_MAP[h.toStatus];
                            return (
                                <div key={h.id} style={{ display: 'flex', alignItems: 'flex-start', gap: '12px', fontSize: '13px' }}>
                                    <div style={{ width: '8px', height: '8px', borderRadius: '50%', background: toInfo?.color ?? '#6b7a8d', marginTop: '4px', flexShrink: 0 }} />
                                    <div>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                            {h.fromStatus && <span style={{ color: 'var(--text-muted)' }}>{STATUS_MAP[h.fromStatus]?.label ?? h.fromStatus}</span>}
                                            {h.fromStatus && <span style={{ color: 'var(--text-muted)' }}>→</span>}
                                            <span style={{ fontWeight: 700, color: toInfo?.color ?? 'var(--text-primary)' }}>{toInfo?.label ?? h.toStatus}</span>
                                            <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{fmtDateTime(h.changedAt)}</span>
                                        </div>
                                        {h.reason && <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>"{h.reason}"</div>}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}

            {/* ─── Send Modal ───────────────────────────────────────────────────── */}
            <AccessibleModal open={showSend} onClose={() => setShowSend(false)} title="Enviar Presupuesto" maxWidth="420px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowSend(false)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={handleSend} disabled={sending}>{sending ? 'Enviando...' : '📧 Enviar'}</button>
                </div>)}>
                        {formError && (
                            <div style={{ padding: '10px 12px', marginBottom: 14, borderRadius: 8, color: 'var(--danger)', background: 'var(--danger-bg)', fontSize: 13 }}>
                                {formError}
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
                                    onChange={e => setSendForm({ ...sendForm, toName: e.target.value })} />
                            </div>
                            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', cursor: 'pointer' }}>
                                <input type="checkbox" checked={sendForm.attachPdf} onChange={e => setSendForm({ ...sendForm, attachPdf: e.target.checked })} />
                                Adjuntar PDF
                            </label>
                        </div>
            </AccessibleModal>

            <AccessibleModal open={showReject} onClose={() => setShowReject(false)} title="Rechazar Presupuesto" maxWidth="400px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowReject(false)}>Cancelar</button>
                    <button style={{ padding: '8px 16px', borderRadius: '7px', border: 'none', cursor: 'pointer', fontWeight: 600, background: 'var(--danger-bg)', color: 'var(--danger)' }}
                        onClick={handleReject}>Confirmar</button>
                </div>)}>
                        <div className="form-group" style={{ marginBottom: '20px' }}>
                            <label className="erp-label">MOTIVO</label>
                            <textarea className="erp-input" rows={3} value={rejectReason} onChange={e => setRejectReason(e.target.value)}
                                placeholder="Motivo del rechazo (opcional)" style={{ resize: 'vertical' }} />
                        </div>
            </AccessibleModal>

            <AccessibleModal open={showConvert} onClose={() => setShowConvert(false)} title="Convertir a Factura" maxWidth="380px"
                footer={(<div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                    <button className="btn btn-secondary" onClick={() => setShowConvert(false)}>Cancelar</button>
                    <button className="btn btn-primary" onClick={handleConvert}>✓ Convertir</button>
                </div>)}>
                        <div style={{ background: 'var(--surface-2)', borderRadius: '8px', padding: '10px 14px', marginBottom: '14px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                            Se creará una factura por {fmt(quote.totalAmount)} para <strong>{quote.clientName}</strong>.
                        </div>
                        <div className="form-group" style={{ marginBottom: '20px' }}>
                            <label className="erp-label">VENCIMIENTO DE LA FACTURA</label>
                            <input type="date" className="erp-input" value={convertDueDate} onChange={e => setConvertDueDate(e.target.value)} />
                        </div>
            </AccessibleModal>
        </div>
    );
}

function TotalRow({ label, value, valueColor }: { label: string; value: string; valueColor?: string }) {
    return (
        <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)' }}>
            <span>{label}</span>
            <span style={{ fontWeight: 600, color: valueColor }}>{value}</span>
        </div>
    );
}
