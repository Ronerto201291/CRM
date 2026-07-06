'use client';
import { useState } from 'react';
import { useRouter } from 'next/navigation';
import AccessibleModal from '@/components/AccessibleModal';
import { PAYMENT_METHODS, type PaymentMethodValue } from '@/lib/paymentMethods';

interface InvoiceLine {
    id: string; description: string; quantity: number;
    unitPrice: number; taxRate: number; surchargeRate: number;
    lineTotal: number;
}
interface Invoice {
    id: string; number: string; series: string; fiscalYear: number;
    invoiceType: string; issueDate: string; dueDate: string;
    subtotal: number; taxAmount: number; irpfRate: number; irpfAmount: number;
    surchargeAmount: number; total: number; status: string; isLocked: boolean;
    lockedAt?: string; hash?: string; verifactuQrUrl?: string;
    clientId: string; clientName?: string; clientTaxId?: string; clientAddress?: string;
    companyNif?: string; companyName?: string; companyAddress?: string;
    journalEntryId?: string; publicViewUrl?: string;
    invoiceLines: InvoiceLine[];
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Draft: { label: 'Borrador', cls: 'badge-gray' },
    Issued: { label: 'Emitida', cls: 'badge-info' },
    Paid: { label: 'Pagada', cls: 'badge-success' },
    Locked: { label: '🔒 Bloqueada', cls: 'badge-danger' },
};

interface InvoiceDetailClientProps {
    id: string;
    initialInvoice: Invoice | null;
}

export default function InvoiceDetailClient({ id, initialInvoice }: InvoiceDetailClientProps) {
    const router = useRouter();
    const [invoice, setInvoice] = useState<Invoice | null>(initialInvoice);
    const [loading, setLoading] = useState(false);
    const [printing, setPrinting] = useState(false);
    const [linkCopied, setLinkCopied] = useState(false);
    const [showPayModal, setShowPayModal] = useState(false);
    const [paymentMethod, setPaymentMethod] = useState<PaymentMethodValue>('bank');
    const [paying, setPaying] = useState(false);

    const load = async () => {
        setLoading(true);
        const r = await fetch(`/api/proxy/invoices/${id}`);
        if (r.ok) setInvoice(await r.json());
        setLoading(false);
    };

    const markPaid = async () => {
        setPaying(true);
        try {
            await fetch(`/api/proxy/invoices/${id}/pay`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ paymentMethod }),
            });
            setShowPayModal(false);
            await load();
        } finally {
            setPaying(false);
        }
    };

    const lockInvoice = async () => {
        if (!confirm('¿Bloquear y contabilizar? Esta acción es IRREVERSIBLE.')) return;
        await fetch(`/api/proxy/invoices/${id}/lock`, { method: 'POST' }); load();
    };

    const printPdf = () => {
        setPrinting(true);
        setTimeout(() => { window.print(); setPrinting(false); }, 300);
    };

    const copyPublicLink = async () => {
        if (!invoice?.publicViewUrl) return;
        await navigator.clipboard.writeText(invoice.publicViewUrl);
        setLinkCopied(true);
        setTimeout(() => setLinkCopied(false), 2000);
    };

    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;
    const fmtDate = (d: string) => d ? new Date(d).toLocaleDateString('es-ES', { day: '2-digit', month: 'long', year: 'numeric' }) : '—';

    if (loading) return <div style={{ padding: '40px', fontFamily: 'Inter, sans-serif', color: 'var(--text-muted)' }}>Cargando factura...</div>;
    if (!invoice) return <div style={{ padding: '40px', fontFamily: 'Inter, sans-serif', color: 'var(--danger)' }}>Factura no encontrada</div>;

    const st = STATUS_MAP[invoice.status] ?? { label: invoice.status, cls: 'badge-gray' };

    return (
        <div style={{ padding: '28px 32px', fontFamily: 'Inter, sans-serif' }} id="invoice-detail">
            {/* Print-hide header */}
            <div className="no-print" style={{ marginBottom: '24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <button onClick={() => router.push('/billing')} style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--brand-primary)', fontSize: '14px', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '6px' }}>
                    ← Volver a Facturación
                </button>
                <div style={{ display: 'flex', gap: '10px' }}>
                    {!invoice.isLocked && invoice.status !== 'Paid' && (
                        <button className="btn btn-success btn-sm" onClick={() => setShowPayModal(true)}>✓ Marcar como Pagada</button>
                    )}
                    {!invoice.isLocked && (
                        <button className="btn btn-sm" style={{ background: 'var(--danger-bg)', color: 'var(--danger)' }} onClick={lockInvoice}>🔒 Bloquear y Contabilizar</button>
                    )}
                    <button className="btn btn-secondary btn-sm" onClick={printPdf} disabled={printing}>
                        🖨️ {printing ? 'Preparando...' : 'Imprimir / PDF'}
                    </button>
                    {invoice.publicViewUrl && (
                        <button className="btn btn-secondary btn-sm" onClick={copyPublicLink}>
                            🔗 {linkCopied ? 'Enlace copiado' : 'Copiar enlace para el cliente'}
                        </button>
                    )}
                </div>
            </div>

            {/* Invoice document */}
            <div className="erp-card" style={{ padding: '40px' }} id="invoice-document">
                {/* Header */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '32px', marginBottom: '36px' }}>
                    <div>
                        <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '4px' }}>Facturado por</div>
                        <div style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-primary)' }}>{invoice.companyName || 'Empresa Emisora'}</div>
                        <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginTop: '4px' }}>NIF: {invoice.companyNif || '—'}</div>
                        {invoice.companyAddress && <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginTop: '2px' }}>{invoice.companyAddress}</div>}
                    </div>
                    <div style={{ textAlign: 'right' }}>
                        <div style={{ fontSize: '28px', fontWeight: 900, color: 'var(--brand-primary)', letterSpacing: '-0.5px' }}>{invoice.number}</div>
                        <span className={`badge ${st.cls}`} style={{ marginTop: '8px', display: 'inline-block' }}>{st.label}</span>
                    </div>
                </div>

                {/* Meta */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', background: 'var(--surface-2)', borderRadius: '10px', padding: '20px', marginBottom: '32px' }}>
                    <div>
                        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>Fecha emisión</div>
                        <div style={{ fontSize: '13px', fontWeight: 600 }}>{fmtDate(invoice.issueDate)}</div>
                    </div>
                    <div>
                        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>Vencimiento</div>
                        <div style={{ fontSize: '13px', fontWeight: 600 }}>{fmtDate(invoice.dueDate)}</div>
                    </div>
                    <div>
                        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>Tipo</div>
                        <div style={{ fontSize: '13px', fontWeight: 600 }}>{invoice.invoiceType}</div>
                    </div>
                    <div>
                        <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>Ejercicio</div>
                        <div style={{ fontSize: '13px', fontWeight: 600 }}>{invoice.fiscalYear}</div>
                    </div>
                </div>

                {/* Client */}
                <div style={{ marginBottom: '32px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: '8px' }}>Facturado a</div>
                    <div style={{ fontSize: '16px', fontWeight: 700 }}>{invoice.clientName || '—'}</div>
                    {invoice.clientTaxId && <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginTop: '2px' }}>NIF/CIF: {invoice.clientTaxId}</div>}
                    {invoice.clientAddress && <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginTop: '2px' }}>{invoice.clientAddress}</div>}
                </div>

                {/* Lines */}
                <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: '32px' }}>
                    <thead>
                        <tr style={{ borderBottom: '2px solid var(--border)' }}>
                            <th style={{ padding: '10px 8px', textAlign: 'left', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Descripción</th>
                            <th style={{ padding: '10px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', width: '70px' }}>Cant.</th>
                            <th style={{ padding: '10px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', width: '110px' }}>P. Unit.</th>
                            <th style={{ padding: '10px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', width: '70px' }}>IVA</th>
                            <th style={{ padding: '10px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', width: '110px' }}>Total</th>
                        </tr>
                    </thead>
                    <tbody>
                        {invoice.invoiceLines?.map((line, i) => (
                            <tr key={i} style={{ borderBottom: '1px solid var(--surface-2)' }}>
                                <td style={{ padding: '12px 8px', fontSize: '14px' }}>{line.description}</td>
                                <td style={{ padding: '12px 8px', textAlign: 'right', fontSize: '13px', color: 'var(--text-secondary)' }}>{line.quantity}</td>
                                <td style={{ padding: '12px 8px', textAlign: 'right', fontSize: '13px' }}>{fmt(line.unitPrice)}</td>
                                <td style={{ padding: '12px 8px', textAlign: 'right', fontSize: '13px', color: 'var(--text-secondary)' }}>{line.taxRate}%</td>
                                <td style={{ padding: '12px 8px', textAlign: 'right', fontSize: '13px', fontWeight: 600 }}>{fmt(line.lineTotal || line.quantity * line.unitPrice * (1 + line.taxRate / 100))}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>

                {/* Totals */}
                <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '32px' }}>
                    <div style={{ width: '280px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid var(--border)', fontSize: '13px' }}>
                            <span style={{ color: 'var(--text-secondary)' }}>Base imponible</span>
                            <span style={{ fontWeight: 600 }}>{fmt(invoice.subtotal)}</span>
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid var(--border)', fontSize: '13px' }}>
                            <span style={{ color: 'var(--text-secondary)' }}>IVA</span>
                            <span style={{ fontWeight: 600 }}>{fmt(invoice.taxAmount)}</span>
                        </div>
                        {invoice.irpfRate > 0 && (
                            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid var(--border)', fontSize: '13px' }}>
                                <span style={{ color: 'var(--text-secondary)' }}>IRPF ({invoice.irpfRate}%)</span>
                                <span style={{ fontWeight: 600, color: 'var(--danger)' }}>– {fmt(invoice.irpfAmount)}</span>
                            </div>
                        )}
                        <div style={{ display: 'flex', justifyContent: 'space-between', padding: '14px 0', fontSize: '18px' }}>
                            <span style={{ fontWeight: 800 }}>TOTAL</span>
                            <span style={{ fontWeight: 900, color: 'var(--brand-primary)' }}>{fmt(invoice.total)}</span>
                        </div>
                    </div>
                </div>

                {/* Compliance footer */}
                <div style={{ borderTop: '1px solid var(--border)', paddingTop: '20px', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', lineHeight: '1.6' }}>
                        <div>Factura emitida conforme a RD 1619/2012 · Ley 11/2021 Antifraude</div>
                        {invoice.hash && <div style={{ marginTop: '4px' }}>SHA-256: <code style={{ fontFamily: 'monospace', fontSize: '10px' }}>{invoice.hash.slice(0, 32)}...</code></div>}
                        {invoice.isLocked && invoice.lockedAt && <div style={{ marginTop: '2px' }}>Bloqueada: {fmtDate(invoice.lockedAt)}</div>}
                        {invoice.journalEntryId && <div style={{ marginTop: '2px', color: 'var(--success)' }}>✓ Contabilizada</div>}
                    </div>
                    {invoice.verifactuQrUrl && (
                        <div style={{ textAlign: 'center' }}>
                            {/* eslint-disable-next-line @next/next/no-img-element */}
                            <img
                                src={`https://api.qrserver.com/v1/create-qr-code/?size=80x80&data=${encodeURIComponent(invoice.verifactuQrUrl)}`}
                                alt="Verifactu QR"
                                width={80} height={80}
                                style={{ border: '1px solid var(--border)', borderRadius: '4px' }}
                            />
                            <div style={{ fontSize: '9px', color: 'var(--text-muted)', marginTop: '4px' }}>Verifactu AEAT</div>
                        </div>
                    )}
                </div>
            </div>

            <AccessibleModal
                open={showPayModal}
                onClose={() => setShowPayModal(false)}
                title="Marcar factura como pagada"
                maxWidth="420px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary" onClick={() => setShowPayModal(false)}>Cancelar</button>
                        <button className="btn btn-success" onClick={markPaid} disabled={paying}>
                            {paying ? 'Registrando...' : '✓ Confirmar pago'}
                        </button>
                    </div>
                )}
            >
                <label className="erp-label">FORMA DE PAGO</label>
                <select className="erp-input" value={paymentMethod} onChange={e => setPaymentMethod(e.target.value as PaymentMethodValue)}>
                    {PAYMENT_METHODS.map(m => (
                        <option key={m.value} value={m.value}>{m.label}</option>
                    ))}
                </select>
                <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '12px' }}>
                    Se registrará el cobro y se generará el asiento en tesorería según la cuenta PGC asociada.
                </p>
            </AccessibleModal>

            <style>{`
        @media print {
          .no-print { display: none !important; }
          body { background: white !important; }
          #invoice-document { box-shadow: none !important; border: none !important; }
        }
      `}</style>
        </div>
    );
}
