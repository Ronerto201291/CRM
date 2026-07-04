'use client';
import { useEffect, useState, useCallback, Suspense } from 'react';
import { useParams, useSearchParams } from 'next/navigation';

interface InvoiceLine {
    description: string; quantity: number; unitPrice: number; taxRate: number; lineTotal: number;
}
interface PublicInvoice {
    number: string; series: string; fiscalYear: number; status: string; isLocked: boolean;
    companyName: string; companyAddress?: string;
    issueDate: string; dueDate: string;
    subtotal: number; taxAmount: number; irpfAmount: number; surchargeAmount: number; total: number;
    lines: InvoiceLine[];
}

const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;
const fmtDate = (s: string) => new Date(s).toLocaleDateString('es-ES', { day: '2-digit', month: 'long', year: 'numeric' });

const PRIMARY = '#1B3A6B';
const PRIMARY_BG = '#EAF0FB';
const SUCCESS = '#15803d';
const DANGER = '#b91c1c';

const STATUS_STYLE: Record<string, { label: string; bg: string }> = {
    Paid: { label: '✓ PAGADA', bg: SUCCESS },
    Locked: { label: 'EMITIDA', bg: PRIMARY },
    Issued: { label: 'EMITIDA', bg: PRIMARY },
    Draft: { label: 'BORRADOR', bg: '#94a3b8' },
};

export default function PublicInvoicePage() {
    return (
        <Suspense fallback={
            <div style={{ minHeight: '100vh', background: '#f8fafc', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'Inter, sans-serif' }}>
                <div style={{ textAlign: 'center', color: '#6b7a8d' }}>
                    <div style={{ fontSize: '32px', marginBottom: '12px' }}>⏳</div>
                    <div>Cargando factura...</div>
                </div>
            </div>
        }>
            <PublicInvoiceView />
        </Suspense>
    );
}

function PublicInvoiceView() {
    const { token } = useParams<{ token: string }>();
    const searchParams = useSearchParams();
    const pago = searchParams.get('pago');
    const [invoice, setInvoice] = useState<PublicInvoice | null>(null);
    const [loading, setLoading] = useState(true);
    const [notFound, setNotFound] = useState(false);
    const [paying, setPaying] = useState(false);
    const [payError, setPayError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        const r = await fetch(`/api/proxy/v1/public/invoice-view/${token}`);
        if (r.ok) {
            const data = await r.json();
            setInvoice(data);
        } else if (r.status === 404) {
            setNotFound(true);
        }
        setLoading(false);
    }, [token]);

    useEffect(() => { load(); }, [load]);

    const handlePay = async () => {
        setPaying(true);
        setPayError(null);
        try {
            const r = await fetch(`/api/proxy/v1/public/invoice-view/${token}/checkout`, { method: 'POST' });
            const data = await r.json();
            if (r.ok && data.checkoutUrl) {
                window.location.href = data.checkoutUrl;
                return;
            }
            setPayError(data.error || 'No se pudo iniciar el pago. Inténtalo de nuevo.');
        } catch {
            setPayError('No se pudo iniciar el pago. Inténtalo de nuevo.');
        }
        setPaying(false);
    };

    if (loading) {
        return (
            <div style={{ minHeight: '100vh', background: '#f8fafc', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'Inter, sans-serif' }}>
                <div style={{ textAlign: 'center', color: '#6b7a8d' }}>
                    <div style={{ fontSize: '32px', marginBottom: '12px' }}>⏳</div>
                    <div>Cargando factura...</div>
                </div>
            </div>
        );
    }

    if (notFound || !invoice) {
        return (
            <div style={{ minHeight: '100vh', background: '#f8fafc', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'Inter, sans-serif' }}>
                <div style={{ textAlign: 'center', maxWidth: '400px', padding: '40px' }}>
                    <div style={{ fontSize: '48px', marginBottom: '16px' }}>🔍</div>
                    <h1 style={{ fontSize: '20px', fontWeight: 700, marginBottom: '8px', color: '#1c2b3a' }}>Factura no encontrada</h1>
                    <p style={{ color: '#6b7a8d', fontSize: '14px' }}>El enlace puede ser incorrecto. Contacta con la empresa emisora para obtener una copia.</p>
                </div>
            </div>
        );
    }

    const statusStyle = STATUS_STYLE[invoice.status] ?? { label: invoice.status, bg: '#64748b' };

    return (
        <div style={{ minHeight: '100vh', background: '#f1f5f9', fontFamily: 'Inter, sans-serif', padding: '24px 16px' }}>
            <div style={{ maxWidth: '860px', margin: '0 auto' }}>

                {/* Header banner */}
                <div style={{ background: PRIMARY, borderRadius: '12px 12px 0 0', padding: '24px 28px', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                    <div>
                        <div style={{ color: '#B0C4DE', fontSize: '11px', fontWeight: 700, letterSpacing: '0.1em', textTransform: 'uppercase', marginBottom: '6px' }}>FACTURA</div>
                        <div style={{ color: 'white', fontSize: '22px', fontWeight: 800, fontFamily: 'monospace' }}>{invoice.number}</div>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                        <div style={{ background: statusStyle.bg, color: 'white', padding: '6px 16px', borderRadius: '20px', fontSize: '13px', fontWeight: 700 }}>{statusStyle.label}</div>
                        <div style={{ color: '#B0C4DE', fontSize: '12px', marginTop: '8px' }}>
                            Vencimiento <strong style={{ color: 'white' }}>{fmtDate(invoice.dueDate)}</strong>
                        </div>
                    </div>
                </div>

                {pago === 'exito' && invoice.status !== 'Paid' && (
                    <div style={{ background: '#ecfdf5', borderLeft: `4px solid ${SUCCESS}`, padding: '14px 28px', fontSize: '13px', color: '#065f46' }}>
                        ✓ Pago recibido. Puede tardar unos segundos en reflejarse como pagada — recarga la página si no ves el cambio.
                    </div>
                )}
                {pago === 'cancelado' && (
                    <div style={{ background: '#fef2f2', borderLeft: `4px solid ${DANGER}`, padding: '14px 28px', fontSize: '13px', color: '#7f1d1d' }}>
                        Pago cancelado. Puedes intentarlo de nuevo cuando quieras.
                    </div>
                )}

                {/* Company info */}
                <div style={{ background: PRIMARY_BG, padding: '20px 28px', borderBottom: '1px solid #C8D4E0' }}>
                    <div style={{ fontSize: '10px', fontWeight: 700, color: '#6B7A8D', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '8px' }}>EMPRESA EMISORA</div>
                    <div style={{ fontWeight: 800, fontSize: '15px', color: '#1c2b3a', marginBottom: '4px' }}>{invoice.companyName}</div>
                    {invoice.companyAddress && <div style={{ fontSize: '13px', color: '#6B7A8D' }}>{invoice.companyAddress}</div>}
                    <div style={{ marginTop: '10px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px', fontSize: '12px', maxWidth: '360px' }}>
                        <div><span style={{ color: '#6B7A8D' }}>Emisión: </span><strong>{fmtDate(invoice.issueDate)}</strong></div>
                        <div><span style={{ color: '#6B7A8D' }}>Vencimiento: </span><strong>{fmtDate(invoice.dueDate)}</strong></div>
                    </div>
                </div>

                {/* Lines table */}
                <div style={{ background: 'white', padding: '0', overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                        <thead>
                            <tr style={{ background: PRIMARY }}>
                                {['DESCRIPCIÓN', 'CANT.', 'P.UNIT.', 'IVA%', 'TOTAL'].map((h, i) => (
                                    <th key={h} style={{ padding: '10px 12px', textAlign: i >= 1 ? 'right' : 'left', color: 'white', fontSize: '10px', fontWeight: 700, letterSpacing: '0.06em' }}>{h}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {invoice.lines.map((line, i) => (
                                <tr key={i} style={{ background: i % 2 === 0 ? 'white' : '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
                                    <td style={{ padding: '10px 12px', fontWeight: 500 }}>{line.description}</td>
                                    <td style={{ padding: '10px 8px', textAlign: 'right' }}>{line.quantity.toLocaleString('es-ES', { maximumFractionDigits: 2 })}</td>
                                    <td style={{ padding: '10px 8px', textAlign: 'right' }}>{fmt(line.unitPrice)}</td>
                                    <td style={{ padding: '10px 8px', textAlign: 'right', color: '#6B7A8D' }}>{line.taxRate}%</td>
                                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 700 }}>{fmt(line.lineTotal)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>

                {/* Totals */}
                <div style={{ background: 'white', padding: '20px 28px', display: 'flex', justifyContent: 'flex-end', borderTop: '1px solid #e2e8f0', borderRadius: '0 0 12px 12px' }}>
                    <div style={{ width: '280px' }}>
                        <Row label="Base imponible" value={fmt(invoice.subtotal)} />
                        <Row label="IVA" value={fmt(invoice.taxAmount)} />
                        {invoice.irpfAmount > 0 && <Row label="IRPF" value={`– ${fmt(invoice.irpfAmount)}`} color={DANGER} />}
                        {invoice.surchargeAmount > 0 && <Row label="Recargo equivalencia" value={fmt(invoice.surchargeAmount)} />}
                        <div style={{ background: PRIMARY, borderRadius: '8px', padding: '12px 16px', marginTop: '10px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <span style={{ color: 'white', fontWeight: 700, fontSize: '14px' }}>TOTAL</span>
                            <span style={{ color: 'white', fontWeight: 800, fontSize: '20px' }}>{fmt(invoice.total)}</span>
                        </div>

                        {invoice.isLocked && invoice.status !== 'Paid' && (
                            <div style={{ marginTop: '14px' }}>
                                <button
                                    onClick={handlePay}
                                    disabled={paying}
                                    style={{
                                        width: '100%', background: SUCCESS, color: 'white', border: 'none', borderRadius: '8px',
                                        padding: '12px 16px', fontWeight: 700, fontSize: '14px', cursor: paying ? 'default' : 'pointer',
                                        opacity: paying ? 0.7 : 1,
                                    }}
                                >
                                    {paying ? 'Redirigiendo a Stripe…' : '💳 Pagar ahora'}
                                </button>
                                {payError && (
                                    <div style={{ color: DANGER, fontSize: '12px', marginTop: '8px', textAlign: 'right' }}>{payError}</div>
                                )}
                            </div>
                        )}
                    </div>
                </div>

                {/* Footer */}
                <div style={{ textAlign: 'center', padding: '20px', fontSize: '11px', color: '#94a3b8', lineHeight: 1.6 }}>
                    Factura emitida por <strong>{invoice.companyName}</strong><br />
                    Precios en EUR · Documento conforme a RD 1619/2012 y Ley 11/2021 Antifraude
                </div>
            </div>
        </div>
    );
}

function Row({ label, value, color }: { label: string; value: string; color?: string }) {
    return (
        <div style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', fontSize: '13px' }}>
            <span style={{ color: '#6B7A8D' }}>{label}</span>
            <span style={{ fontWeight: 600, color: color ?? '#1c2b3a' }}>{value}</span>
        </div>
    );
}
