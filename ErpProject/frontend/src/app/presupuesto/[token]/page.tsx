'use client';
import { useEffect, useState, useCallback } from 'react';
import { useParams } from 'next/navigation';

interface LineItem {
    sortOrder: number; description: string; productCode?: string; unit?: string;
    quantity: number; unitPrice: number; discountPct: number;
    lineTaxBase: number; taxRate: number; lineTaxAmount: number; lineTotalAmount: number;
}
interface TaxGroup { taxRate: number; baseAmount: number; taxAmount: number; }
interface PublicQuote {
    number: string; seriesPrefix: string; fiscalYear: number; version: number; status: string;
    clientName?: string; clientTaxId?: string; clientEmail?: string;
    issueDate: string; validUntil: string;
    globalDiscountPct: number; globalDiscountAmount: number;
    subtotalBeforeDisc: number; taxBaseAmount: number; taxAmount: number; totalAmount: number;
    notes?: string; currency: string;
    companyName: string; companyTaxId?: string; companyEmail?: string; companyPhone?: string; companyAddress?: string;
    lines: LineItem[];
    taxBreakdown: TaxGroup[];
}

const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;
const fmtDate = (s: string) => new Date(s).toLocaleDateString('es-ES', { day: '2-digit', month: 'long', year: 'numeric' });

const PRIMARY = '#1B3A6B';
const PRIMARY_BG = '#EAF0FB';
const SUCCESS = '#15803d';
const DANGER = '#b91c1c';

export default function PublicQuotePage() {
    const { token } = useParams<{ token: string }>();
    const [quote, setQuote] = useState<PublicQuote | null>(null);
    const [loading, setLoading] = useState(true);
    const [notFound, setNotFound] = useState(false);

    // Accept flow
    const [showAccept, setShowAccept] = useState(false);
    const [accepted, setAccepted] = useState(false);
    const [accepting, setAccepting] = useState(false);

    // Reject flow
    const [showReject, setShowReject] = useState(false);
    const [rejectReason, setRejectReason] = useState('');
    const [rejected, setRejected] = useState(false);
    const [rejecting, setRejecting] = useState(false);

    const load = useCallback(async () => {
        setLoading(true);
        const r = await fetch(`/api/proxy/v1/public/quotes/${token}`);
        if (r.ok) {
            const data = await r.json();
            setQuote(data);
        } else if (r.status === 404) {
            setNotFound(true);
        }
        setLoading(false);
    }, [token]);

    useEffect(() => { load(); }, [load]);

    const handleAccept = async () => {
        setAccepting(true);
        try {
            const res = await fetch(`/api/proxy/v1/public/quotes/${token}/accept`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({}),
            });
            if (res.ok) { setShowAccept(false); setAccepted(true); load(); }
            else { const e = await res.json(); alert(e.error || 'Error al procesar'); }
        } finally { setAccepting(false); }
    };

    const handleReject = async () => {
        setRejecting(true);
        try {
            const res = await fetch(`/api/proxy/v1/public/quotes/${token}/reject`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ reason: rejectReason }),
            });
            if (res.ok) { setShowReject(false); setRejected(true); load(); }
            else { const e = await res.json(); alert(e.error || 'Error al procesar'); }
        } finally { setRejecting(false); }
    };

    // ── Loading / Error states ────────────────────────────────────────────────
    if (loading) {
        return (
            <div style={{ minHeight: '100vh', background: '#f8fafc', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'Inter, sans-serif' }}>
                <div style={{ textAlign: 'center', color: '#6b7a8d' }}>
                    <div style={{ fontSize: '32px', marginBottom: '12px' }}>⏳</div>
                    <div>Cargando presupuesto...</div>
                </div>
            </div>
        );
    }

    if (notFound || !quote) {
        return (
            <div style={{ minHeight: '100vh', background: '#f8fafc', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'Inter, sans-serif' }}>
                <div style={{ textAlign: 'center', maxWidth: '400px', padding: '40px' }}>
                    <div style={{ fontSize: '48px', marginBottom: '16px' }}>🔍</div>
                    <h1 style={{ fontSize: '20px', fontWeight: 700, marginBottom: '8px', color: '#1c2b3a' }}>Presupuesto no encontrado</h1>
                    <p style={{ color: '#6b7a8d', fontSize: '14px' }}>El enlace puede haber expirado o ser incorrecto. Contacta con la empresa para obtener un nuevo presupuesto.</p>
                </div>
            </div>
        );
    }

    const isExpired = new Date(quote.validUntil) < new Date() && quote.status === 'Sent';
    const isFinal = ['Accepted', 'Rejected', 'Expired', 'Converted'].includes(quote.status);

    return (
        <div style={{ minHeight: '100vh', background: '#f1f5f9', fontFamily: 'Inter, sans-serif', padding: '24px 16px' }}>
            <div style={{ maxWidth: '860px', margin: '0 auto' }}>

                {/* Header banner */}
                <div style={{ background: PRIMARY, borderRadius: '12px 12px 0 0', padding: '24px 28px', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                    <div>
                        <div style={{ color: '#B0C4DE', fontSize: '11px', fontWeight: 700, letterSpacing: '0.1em', textTransform: 'uppercase', marginBottom: '6px' }}>PRESUPUESTO</div>
                        <div style={{ color: 'white', fontSize: '22px', fontWeight: 800, fontFamily: 'monospace' }}>{quote.number}</div>
                        {quote.version > 1 && <div style={{ color: '#B0C4DE', fontSize: '12px', marginTop: '2px' }}>Versión {quote.version}</div>}
                    </div>
                    <div style={{ textAlign: 'right' }}>
                        {quote.status === 'Accepted' || accepted ? (
                            <div style={{ background: SUCCESS, color: 'white', padding: '6px 16px', borderRadius: '20px', fontSize: '13px', fontWeight: 700 }}>✓ ACEPTADO</div>
                        ) : quote.status === 'Rejected' || rejected ? (
                            <div style={{ background: DANGER, color: 'white', padding: '6px 16px', borderRadius: '20px', fontSize: '13px', fontWeight: 700 }}>✕ RECHAZADO</div>
                        ) : quote.status === 'Converted' ? (
                            <div style={{ background: '#6d28d9', color: 'white', padding: '6px 16px', borderRadius: '20px', fontSize: '13px', fontWeight: 700 }}>CONVERTIDO</div>
                        ) : isExpired ? (
                            <div style={{ background: '#b45309', color: 'white', padding: '6px 16px', borderRadius: '20px', fontSize: '13px', fontWeight: 700 }}>EXPIRADO</div>
                        ) : (
                            <div style={{ background: '#0369a1', color: 'white', padding: '6px 16px', borderRadius: '20px', fontSize: '13px', fontWeight: 700 }}>PENDIENTE</div>
                        )}
                        <div style={{ color: '#B0C4DE', fontSize: '12px', marginTop: '8px' }}>
                            Válido hasta <strong style={{ color: isExpired ? '#fca5a5' : 'white' }}>{fmtDate(quote.validUntil)}</strong>
                        </div>
                    </div>
                </div>

                {/* Company + Client info */}
                <div style={{ background: PRIMARY_BG, padding: '20px 28px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '24px', borderBottom: '1px solid #C8D4E0' }}>
                    <div>
                        <div style={{ fontSize: '10px', fontWeight: 700, color: '#6B7A8D', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '8px' }}>EMPRESA EMISORA</div>
                        <div style={{ fontWeight: 800, fontSize: '15px', color: '#1c2b3a', marginBottom: '4px' }}>{quote.companyName}</div>
                        {quote.companyTaxId && <div style={{ fontSize: '13px', color: '#6B7A8D' }}>NIF/CIF: {quote.companyTaxId}</div>}
                        {quote.companyEmail && <div style={{ fontSize: '13px', color: '#6B7A8D' }}>{quote.companyEmail}</div>}
                        {quote.companyPhone && <div style={{ fontSize: '13px', color: '#6B7A8D' }}>{quote.companyPhone}</div>}
                        {quote.companyAddress && <div style={{ fontSize: '13px', color: '#6B7A8D' }}>{quote.companyAddress}</div>}
                    </div>
                    <div>
                        <div style={{ fontSize: '10px', fontWeight: 700, color: '#6B7A8D', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '8px' }}>DESTINATARIO</div>
                        <div style={{ fontWeight: 700, fontSize: '15px', color: '#1c2b3a', marginBottom: '4px' }}>{quote.clientName || '—'}</div>
                        {quote.clientTaxId && <div style={{ fontSize: '13px', color: '#6B7A8D' }}>NIF/CIF: {quote.clientTaxId}</div>}
                        {quote.clientEmail && <div style={{ fontSize: '13px', color: '#6B7A8D' }}>{quote.clientEmail}</div>}
                        <div style={{ marginTop: '8px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px', fontSize: '12px' }}>
                            <div><span style={{ color: '#6B7A8D' }}>Emisión: </span><strong>{fmtDate(quote.issueDate)}</strong></div>
                            <div><span style={{ color: '#6B7A8D' }}>Válido: </span><strong>{fmtDate(quote.validUntil)}</strong></div>
                        </div>
                    </div>
                </div>

                {/* Lines table */}
                <div style={{ background: 'white', padding: '0', overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                        <thead>
                            <tr style={{ background: PRIMARY }}>
                                {['DESCRIPCIÓN', 'UNID.', 'CANT.', 'P.UNIT.', 'DTO%', 'BASE IMP.', 'IVA%', 'TOTAL'].map((h, i) => (
                                    <th key={h} style={{ padding: '10px 12px', textAlign: i >= 2 ? 'right' : i === 1 ? 'center' : 'left', color: 'white', fontSize: '10px', fontWeight: 700, letterSpacing: '0.06em' }}>{h}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {[...quote.lines].sort((a, b) => a.sortOrder - b.sortOrder).map((line, i) => (
                                <tr key={i} style={{ background: i % 2 === 0 ? 'white' : '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
                                    <td style={{ padding: '10px 12px', fontWeight: 500 }}>
                                        {line.description}
                                        {line.productCode && <span style={{ display: 'block', fontSize: '11px', color: '#94a3b8', fontFamily: 'monospace' }}>{line.productCode}</span>}
                                    </td>
                                    <td style={{ padding: '10px 8px', textAlign: 'center', color: '#6B7A8D' }}>{line.unit || 'ud'}</td>
                                    <td style={{ padding: '10px 8px', textAlign: 'right' }}>{line.quantity.toLocaleString('es-ES', { maximumFractionDigits: 2 })}</td>
                                    <td style={{ padding: '10px 8px', textAlign: 'right' }}>{fmt(line.unitPrice)}</td>
                                    <td style={{ padding: '10px 8px', textAlign: 'right', color: line.discountPct > 0 ? DANGER : '#94a3b8' }}>{line.discountPct > 0 ? `${line.discountPct}%` : '—'}</td>
                                    <td style={{ padding: '10px 8px', textAlign: 'right' }}>{fmt(line.lineTaxBase)}</td>
                                    <td style={{ padding: '10px 8px', textAlign: 'right', color: '#6B7A8D' }}>{line.taxRate}%</td>
                                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 700 }}>{fmt(line.lineTotalAmount)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>

                {/* Tax breakdown + totals */}
                <div style={{ background: 'white', padding: '20px 28px', display: 'grid', gridTemplateColumns: '1fr 240px', gap: '32px', borderTop: '1px solid #e2e8f0' }}>
                    {/* Tax groups */}
                    <div>
                        <div style={{ fontSize: '10px', fontWeight: 700, color: '#6B7A8D', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '10px' }}>DESGLOSE FISCAL</div>
                        <table style={{ borderCollapse: 'collapse', fontSize: '12px' }}>
                            <thead>
                                <tr style={{ background: PRIMARY }}>
                                    {['Tipo IVA', 'Base imp.', 'Cuota IVA'].map(h => (
                                        <th key={h} style={{ padding: '7px 12px', color: 'white', fontSize: '10px', fontWeight: 700, textAlign: h === 'Tipo IVA' ? 'left' : 'right' }}>{h}</th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {quote.taxBreakdown.map(g => (
                                    <tr key={g.taxRate} style={{ borderBottom: '1px solid #e2e8f0' }}>
                                        <td style={{ padding: '7px 12px' }}>{g.taxRate}%</td>
                                        <td style={{ padding: '7px 12px', textAlign: 'right' }}>{fmt(g.baseAmount)}</td>
                                        <td style={{ padding: '7px 12px', textAlign: 'right' }}>{fmt(g.taxAmount)}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>

                        {quote.notes && (
                            <div style={{ marginTop: '16px' }}>
                                <div style={{ fontSize: '10px', fontWeight: 700, color: '#6B7A8D', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '6px' }}>CONDICIONES Y NOTAS</div>
                                <div style={{ fontSize: '13px', color: '#374151', background: '#f8fafc', padding: '10px 14px', borderRadius: '6px', lineHeight: 1.6 }}>{quote.notes}</div>
                            </div>
                        )}
                    </div>

                    {/* Totals */}
                    <div>
                        <div style={{ fontSize: '10px', fontWeight: 700, color: '#6B7A8D', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '10px' }}>RESUMEN</div>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '7px', fontSize: '13px' }}>
                            <Row label="Subtotal bruto" value={fmt(quote.subtotalBeforeDisc + quote.globalDiscountAmount)} />
                            {quote.globalDiscountPct > 0 && (
                                <Row label={`Descuento (${quote.globalDiscountPct}%)`} value={`– ${fmt(quote.globalDiscountAmount)}`} color={DANGER} />
                            )}
                            <Row label="Base imponible" value={fmt(quote.taxBaseAmount)} />
                            <Row label="Total IVA" value={fmt(quote.taxAmount)} />
                        </div>
                        <div style={{ background: PRIMARY, borderRadius: '8px', padding: '12px 16px', marginTop: '12px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <span style={{ color: 'white', fontWeight: 700, fontSize: '14px' }}>TOTAL</span>
                            <span style={{ color: 'white', fontWeight: 800, fontSize: '20px' }}>{fmt(quote.totalAmount)}</span>
                        </div>
                    </div>
                </div>

                {/* CTA — Accept/Reject (only when status is Sent) */}
                {(accepted || quote.status === 'Accepted') && (
                    <div style={{ background: '#dcfce7', border: '1px solid #86efac', borderRadius: '0 0 12px 12px', padding: '28px', textAlign: 'center' }}>
                        <div style={{ fontSize: '40px', marginBottom: '10px' }}>✅</div>
                        <div style={{ fontWeight: 800, fontSize: '18px', color: SUCCESS, marginBottom: '6px' }}>Presupuesto aceptado</div>
                        <div style={{ color: '#16a34a', fontSize: '14px' }}>Nos pondremos en contacto contigo para confirmar los próximos pasos.</div>
                    </div>
                )}
                {(rejected || quote.status === 'Rejected') && (
                    <div style={{ background: '#fee2e2', border: '1px solid #fca5a5', borderRadius: '0 0 12px 12px', padding: '28px', textAlign: 'center' }}>
                        <div style={{ fontSize: '40px', marginBottom: '10px' }}>❌</div>
                        <div style={{ fontWeight: 800, fontSize: '18px', color: DANGER, marginBottom: '6px' }}>Presupuesto rechazado</div>
                        <div style={{ color: '#dc2626', fontSize: '14px' }}>Hemos recibido tu respuesta. Gracias por comunicárnoslo.</div>
                    </div>
                )}
                {quote.status === 'Expired' || (isExpired && !isFinal) ? (
                    <div style={{ background: '#fef3c7', border: '1px solid #fcd34d', borderRadius: '0 0 12px 12px', padding: '28px', textAlign: 'center' }}>
                        <div style={{ fontSize: '40px', marginBottom: '10px' }}>⏰</div>
                        <div style={{ fontWeight: 800, fontSize: '18px', color: '#b45309', marginBottom: '6px' }}>Presupuesto expirado</div>
                        <div style={{ color: '#92400e', fontSize: '14px' }}>Este presupuesto ya no está vigente. Contacta con nosotros para solicitar uno nuevo.</div>
                    </div>
                ) : null}
                {quote.status === 'Converted' && (
                    <div style={{ background: '#ede9fe', border: '1px solid #c4b5fd', borderRadius: '0 0 12px 12px', padding: '28px', textAlign: 'center' }}>
                        <div style={{ fontSize: '40px', marginBottom: '10px' }}>🧾</div>
                        <div style={{ fontWeight: 800, fontSize: '18px', color: '#6d28d9', marginBottom: '6px' }}>Presupuesto convertido a factura</div>
                        <div style={{ color: '#7c3aed', fontSize: '14px' }}>Este presupuesto ya ha sido facturado.</div>
                    </div>
                )}
                {quote.status === 'Sent' && !isFinal && !isExpired && !accepted && !rejected && (
                    <div style={{ background: 'white', borderTop: '1px solid #e2e8f0', borderRadius: '0 0 12px 12px', padding: '28px' }}>
                        <div style={{ textAlign: 'center', marginBottom: '20px' }}>
                            <div style={{ fontSize: '15px', fontWeight: 700, color: '#1c2b3a', marginBottom: '6px' }}>¿Aceptas este presupuesto?</div>
                            <div style={{ fontSize: '13px', color: '#6B7A8D' }}>Al aceptar, confirmas tu acuerdo con las condiciones y el importe de <strong>{fmt(quote.totalAmount)}</strong>.</div>
                        </div>
                        <div style={{ display: 'flex', gap: '12px', justifyContent: 'center' }}>
                            <button
                                onClick={() => setShowAccept(true)}
                                style={{ padding: '12px 32px', borderRadius: '8px', border: 'none', cursor: 'pointer', fontWeight: 700, fontSize: '14px', background: SUCCESS, color: 'white', transition: 'opacity 0.15s' }}>
                                ✓ Aceptar presupuesto
                            </button>
                            <button
                                onClick={() => setShowReject(true)}
                                style={{ padding: '12px 24px', borderRadius: '8px', border: '1px solid #fca5a5', cursor: 'pointer', fontWeight: 600, fontSize: '14px', background: '#fff', color: DANGER, transition: 'all 0.15s' }}>
                                Rechazar
                            </button>
                        </div>
                        <div style={{ textAlign: 'center', marginTop: '14px', fontSize: '11px', color: '#94a3b8' }}>
                            Válido hasta el {fmtDate(quote.validUntil)} · Ley 37/1992 de IVA
                        </div>
                    </div>
                )}

                {/* Footer */}
                <div style={{ textAlign: 'center', padding: '20px', fontSize: '11px', color: '#94a3b8', lineHeight: 1.6 }}>
                    Presupuesto emitido por <strong>{quote.companyName}</strong>{quote.companyTaxId ? ` · NIF/CIF: ${quote.companyTaxId}` : ''}<br />
                    Precios en EUR · IVA desglosado conforme a Ley 37/1992 · Salvo error u omisión
                </div>
            </div>

            {/* ─── Accept Confirmation Modal ────────────────────────────────────── */}
            {showAccept && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 50, padding: '16px' }}>
                    <div style={{ background: 'white', borderRadius: '12px', padding: '28px', maxWidth: '400px', width: '100%', boxShadow: '0 20px 60px rgba(0,0,0,0.2)' }}>
                        <div style={{ textAlign: 'center', marginBottom: '20px' }}>
                            <div style={{ fontSize: '40px', marginBottom: '10px' }}>✅</div>
                            <h2 style={{ fontSize: '18px', fontWeight: 800, marginBottom: '8px' }}>Confirmar aceptación</h2>
                            <p style={{ fontSize: '13px', color: '#6B7A8D', lineHeight: 1.5 }}>
                                Al confirmar, aceptas el presupuesto <strong>{quote.number}</strong> por un importe de <strong>{fmt(quote.totalAmount)}</strong>.
                                Esta acción quedará registrada.
                            </p>
                        </div>
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'center' }}>
                            <button onClick={() => setShowAccept(false)} style={{ padding: '10px 20px', borderRadius: '7px', border: '1px solid #e2e8f0', background: 'white', cursor: 'pointer', fontWeight: 600, color: '#374151' }}>
                                Cancelar
                            </button>
                            <button onClick={handleAccept} disabled={accepting}
                                style={{ padding: '10px 24px', borderRadius: '7px', border: 'none', cursor: 'pointer', fontWeight: 700, background: SUCCESS, color: 'white' }}>
                                {accepting ? 'Procesando...' : '✓ Confirmar aceptación'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* ─── Reject Modal ─────────────────────────────────────────────────── */}
            {showReject && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 50, padding: '16px' }}>
                    <div style={{ background: 'white', borderRadius: '12px', padding: '28px', maxWidth: '400px', width: '100%', boxShadow: '0 20px 60px rgba(0,0,0,0.2)' }}>
                        <h2 style={{ fontSize: '17px', fontWeight: 700, marginBottom: '12px' }}>Rechazar presupuesto</h2>
                        <p style={{ fontSize: '13px', color: '#6B7A8D', marginBottom: '16px' }}>¿Puedes indicarnos el motivo? Esto nos ayuda a mejorar nuestra propuesta.</p>
                        <textarea
                            value={rejectReason}
                            onChange={e => setRejectReason(e.target.value)}
                            placeholder="Motivo del rechazo (opcional)"
                            rows={3}
                            style={{ width: '100%', padding: '10px 12px', borderRadius: '7px', border: '1px solid #e2e8f0', fontSize: '13px', fontFamily: 'Inter, sans-serif', resize: 'vertical', boxSizing: 'border-box', outline: 'none' }}
                        />
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '16px' }}>
                            <button onClick={() => setShowReject(false)} style={{ padding: '10px 20px', borderRadius: '7px', border: '1px solid #e2e8f0', background: 'white', cursor: 'pointer', fontWeight: 600, color: '#374151' }}>
                                Cancelar
                            </button>
                            <button onClick={handleReject} disabled={rejecting}
                                style={{ padding: '10px 20px', borderRadius: '7px', border: 'none', cursor: 'pointer', fontWeight: 700, background: DANGER, color: 'white' }}>
                                {rejecting ? 'Procesando...' : 'Confirmar rechazo'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

function Row({ label, value, color }: { label: string; value: string; color?: string }) {
    return (
        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: '#6B7A8D' }}>{label}</span>
            <span style={{ fontWeight: 600, color: color ?? '#1c2b3a' }}>{value}</span>
        </div>
    );
}
