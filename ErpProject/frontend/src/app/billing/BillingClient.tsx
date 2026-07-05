'use client';
import { useState, useCallback, useEffect } from 'react';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import { updateLineAt } from '@/lib/lineForm';
import { parseListResponse } from '@/lib/parseListResponse';
import { invoiceCreateSchema } from '@/lib/schemas/invoiceCreateSchema';
import { useCachedApi } from '@/hooks/useCachedApi';

interface Invoice {
    id: string; number: string; clientName?: string; issueDate: string;
    subtotal: number; taxAmount: number; irpfAmount: number; total: number;
    status: string; isLocked: boolean; series: string;
}
interface Client { id: string; name: string; taxId: string; }
interface InvoiceLine {
    description: string; quantity: number; unitPrice: number; taxRate: number;
    surchargeRate: number; tipoOperacion: string;
}
type ClientType = 'Registered' | 'Manual';

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Draft: { label: 'Borrador', cls: 'badge-gray' },
    Issued: { label: 'Emitida', cls: 'badge-info' },
    Paid: { label: 'Pagada', cls: 'badge-success' },
    Locked: { label: '🔒 Bloqueada', cls: 'badge-danger' },
};

const emptyLine = (): InvoiceLine => ({
    description: '', quantity: 1, unitPrice: 0, taxRate: 21, surchargeRate: 0, tipoOperacion: 'Nacional',
});

export default function BillingClient({
    initialInvoices,
    initialClients,
}: {
    initialInvoices: Invoice[];
    initialClients: Client[];
}) {
    const [invoices, setInvoices] = useState<Invoice[]>(initialInvoices);
    const [clients, setClients] = useState<Client[]>(initialClients);
    const [showModal, setShowModal] = useState(false);
    const [showChainModal, setShowChainModal] = useState(false);
    const [chainResult, setChainResult] = useState<{ isValid: boolean; totalVerified: number; errorAt?: string } | null>(null);
    const [filter, setFilter] = useState('all');
    const [form, setForm] = useState<{
        clientType: ClientType; clientId: string;
        clientName: string; clientTaxId: string; clientEmail: string; clientAddress: string;
        series: string; dueDate: string; irpfRate: number; invoiceType: string;
        currencyCode: string;
        rectifiedInvoiceId: string;
        rectificationReasonCode: string;
        rectificationReasonText: string;
        validateEuVatWithVies: boolean;
        lines: InvoiceLine[];
    }>({
        clientType: 'Registered', clientId: '',
        clientName: '', clientTaxId: '', clientEmail: '', clientAddress: '',
        series: 'A', dueDate: '', irpfRate: 0, invoiceType: 'Normal', currencyCode: 'EUR',
        rectifiedInvoiceId: '', rectificationReasonCode: '', rectificationReasonText: '',
        validateEuVatWithVies: false,
        lines: [emptyLine()],
    });
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);
    const [pageError, setPageError] = useState<string | null>(null);
    const [currencies, setCurrencies] = useState<{ code: string; name: string; exchangeRate: number }[]>([
        { code: 'EUR', name: 'Euro', exchangeRate: 1 },
    ]);
    const { fetchCached, invalidateCached } = useCachedApi();

    useEffect(() => {
        fetch('/api/proxy/v1/treasury/currencies')
            .then(r => r.ok ? r.json() : [])
            .then((data: { code: string; name: string; exchangeRate: number }[]) => {
                if (Array.isArray(data) && data.length > 0) setCurrencies(data);
            })
            .catch(() => undefined);
    }, []);

    const refresh = useCallback(async () => {
        invalidateCached('invoices');
        invalidateCached('clients');
        const [invData, cliData] = await Promise.all([
            fetchCached<unknown>('invoices?pageSize=500'),
            fetchCached<unknown>('clients?pageSize=500'),
        ]);
        if (invData) setInvoices(parseListResponse<Invoice>(invData));
        if (cliData) setClients(parseListResponse<Client>(cliData));
    }, [fetchCached, invalidateCached]);

    const markPaid = async (id: string) => {
        if (!confirm('¿Marcar esta factura como PAGADA?')) return;
        await fetch(`/api/proxy/invoices/${id}/pay`, { method: 'POST' }); refresh();
    };
    const lockInvoice = async (id: string) => {
        if (!confirm('¿Bloquear y contabilizar esta factura? Esta acción es IRREVERSIBLE.')) return;
        await fetch(`/api/proxy/invoices/${id}/lock`, { method: 'POST' }); refresh();
    };
    const downloadFile = async (url: string, filename: string) => {
        setPageError(null);
        const r = await fetch(url);
        if (!r.ok) {
            setPageError('Error al descargar el archivo');
            return;
        }
        const blob = await r.blob();
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob); a.download = filename;
        document.body.appendChild(a); a.click();
        document.body.removeChild(a); URL.revokeObjectURL(a.href);
    };
    const verifyChain = async () => {
        const year = new Date().getFullYear();
        const r = await fetch(`/api/proxy/invoices/verify-chain?series=A&fiscalYear=${year}`);
        if (r.ok) setChainResult(await r.json());
        setShowChainModal(true);
    };

    const updateLine = <K extends keyof InvoiceLine>(i: number, field: K, val: InvoiceLine[K]) =>
        setForm({ ...form, lines: updateLineAt(form.lines, i, field, val) });
    const addLine = () => setForm({ ...form, lines: [...form.lines, emptyLine()] });
    const removeLine = (i: number) => setForm({ ...form, lines: form.lines.filter((_, idx) => idx !== i) });

    // Totals — round per line, then sum (mirrors BillingHandlers.cs, RD 1619/2012)
    const round2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;
    const subtotal    = form.lines.reduce((s, l) => s + round2(l.quantity * l.unitPrice), 0);
    const taxAmount   = form.lines.reduce((s, l) => s + round2(round2(l.quantity * l.unitPrice) * (l.taxRate / 100)), 0);
    const surcharge   = form.lines.reduce((s, l) => s + round2(round2(l.quantity * l.unitPrice) * (l.surchargeRate / 100)), 0);
    const irpfAmt     = round2(subtotal * (form.irpfRate / 100));
    const total       = round2(subtotal + taxAmount + surcharge - irpfAmt);
    const fxRate = currencies.find(c => c.code === form.currencyCode)?.exchangeRate ?? 1;
    const rateToEur = form.currencyCode === 'EUR' ? 1 : (1 / fxRate);
    const totalEur  = round2(total * rateToEur);

    const emptyForm = () => ({
        clientType: 'Registered' as ClientType, clientId: '',
        clientName: '', clientTaxId: '', clientEmail: '', clientAddress: '',
        series: 'A', dueDate: '', irpfRate: 0, invoiceType: 'Normal', currencyCode: 'EUR',
        rectifiedInvoiceId: '', rectificationReasonCode: '', rectificationReasonText: '',
        validateEuVatWithVies: false,
        lines: [emptyLine()],
    });

    const handleCreate = async () => {
        setFormError(null);
        const parsed = invoiceCreateSchema.safeParse(form);
        if (!parsed.success) {
            setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
            return;
        }
        setSaving(true);
        try {
            const body = {
                ...form,
                clientId: form.clientType === 'Registered' ? form.clientId : null,
                dueDate: form.dueDate || new Date(Date.now() + 30 * 86400000).toISOString(),
                rectifiedInvoiceId: form.rectifiedInvoiceId ? form.rectifiedInvoiceId : null,
                rectificationReasonCode: form.rectificationReasonCode || null,
                rectificationReasonText: form.rectificationReasonText || null,
            };
            const res = await fetch('/api/proxy/invoices', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body),
            });
            if (res.ok) { setShowModal(false); setForm(emptyForm()); refresh(); }
            else {
                const e = await res.json();
                setFormError(e.error || 'Error al crear factura');
            }
        } finally { setSaving(false); }
    };

    const filtered = filter === 'all' ? invoices : invoices.filter(i => i.status === filter);
    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            {/* Header */}
            <div className="page-header">
                <div>
                    <h1 className="page-title">Facturación</h1>
                    <p className="page-subtitle">Gestión de facturas · RD 1619/2012 · Ley 11/2021</p>
                </div>
                <div style={{ display: 'flex', gap: '10px' }}>
                    <button className="btn btn-secondary" onClick={verifyChain} title="Verificar integridad de la cadena SHA-256">
                        🔐 Verificar Cadena
                    </button>
                    <button className="btn btn-primary" onClick={() => { setFormError(null); setForm(emptyForm()); setShowModal(true); }}>
                        <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}><path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" /></svg>
                        Nueva Factura
                    </button>
                </div>
            </div>

            {pageError && (
                <div className="erp-card" style={{ padding: '12px 16px', marginBottom: 16, color: 'var(--danger)', background: 'var(--danger-bg)' }}>
                    {pageError}
                </div>
            )}

            {/* Filter tabs */}
            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px' }}>
                {['all', 'Draft', 'Issued', 'Paid', 'Locked'].map(f => (
                    <button key={f} onClick={() => setFilter(f)}
                        style={{
                            padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)',
                            fontSize: '12px', fontWeight: filter === f ? 700 : 500,
                            background: filter === f ? 'var(--brand-primary)' : 'var(--surface)',
                            color: filter === f ? 'white' : 'var(--text-secondary)',
                            cursor: 'pointer', transition: 'all 0.15s',
                        }}>
                        {f === 'all' ? 'Todas' : STATUS_MAP[f]?.label ?? f}
                        {f !== 'all' && <span style={{ marginLeft: '6px', fontSize: '11px', opacity: 0.7 }}>
                            ({invoices.filter(i => i.status === f).length})
                        </span>}
                    </button>
                ))}
            </div>

            {/* Summary KPIs */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '14px', marginBottom: '20px' }}>
                <MiniStat label="Total Emitido" value={fmt(invoices.reduce((s, i) => s + i.total, 0))} color="var(--brand-primary)" />
                <MiniStat label="Pagado" value={fmt(invoices.filter(i => i.status === 'Paid').reduce((s, i) => s + i.total, 0))} color="var(--success)" />
                <MiniStat label="Pendiente" value={fmt(invoices.filter(i => i.status !== 'Paid' && i.status !== 'Locked').reduce((s, i) => s + i.total, 0))} color="var(--warning)" />
                <MiniStat label="IVA Total" value={fmt(invoices.reduce((s, i) => s + i.taxAmount, 0))} color="#8b5cf6" />
            </div>

            {/* Table */}
            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Cliente</th>
                            <th>Fecha</th>
                            <th style={{ textAlign: 'right' }}>Base</th>
                            <th style={{ textAlign: 'right' }}>IVA</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                            <th>Estado</th>
                            <th style={{ textAlign: 'right' }}>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>
                        {filtered.length === 0 && (
                            <tr><td colSpan={8}>
                                <div className="empty-state">
                                    <div className="empty-state-icon">🧾</div>
                                    <div className="empty-state-title">Sin facturas</div>
                                    <div className="empty-state-sub">Crea tu primera factura con el botón superior</div>
                                </div>
                            </td></tr>
                        )}
                        {filtered.map(inv => (
                            <tr key={inv.id}>
                                <td><a href={`/billing/${inv.id}`} style={{ fontWeight: 700, fontFamily: 'monospace', fontSize: '12px', color: 'var(--brand-primary)', textDecoration: 'none' }}>{inv.number}</a></td>
                                <td style={{ color: 'var(--text-secondary)' }}>{inv.clientName || <span style={{ color: 'var(--text-muted)' }}>—</span>}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{new Date(inv.issueDate).toLocaleDateString('es-ES')}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(inv.subtotal)}</td>
                                <td style={{ textAlign: 'right', color: 'var(--text-secondary)' }}>{fmt(inv.taxAmount)}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(inv.total)}</td>
                                <td><span className={`badge ${STATUS_MAP[inv.status]?.cls ?? 'badge-gray'}`}>{STATUS_MAP[inv.status]?.label ?? inv.status}</span></td>
                                <td style={{ textAlign: 'right' }}>
                                    <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                                        {!inv.isLocked && inv.status !== 'Paid' && (
                                            <button className="btn btn-success btn-sm" onClick={() => markPaid(inv.id)}>Pagar</button>
                                        )}
                                        {!inv.isLocked && (
                                            <button className="btn btn-sm" style={{ background: 'var(--danger-bg)', color: 'var(--danger)' }} onClick={() => lockInvoice(inv.id)}>Bloquear</button>
                                        )}
                                        {inv.isLocked && (
                                            <>
                                                <button className="btn btn-sm btn-secondary" title="Descargar PDF"
                                                    onClick={() => downloadFile(`/api/proxy/invoices/${inv.id}/pdf`, `Factura_${inv.number.replace(/\//g,'-')}.pdf`)}>
                                                    ⬇ PDF
                                                </button>
                                                <button className="btn btn-sm btn-secondary" title="Descargar FacturaE 3.2.2 (Ley Crea y Crece)"
                                                    onClick={() => downloadFile(`/api/proxy/invoices/${inv.id}/facturae`, `FacturaE_${inv.number.replace(/\//g,'-')}.xsig`)}>
                                                    📄 FacturaE
                                                </button>
                                            </>
                                        )}
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {/* New Invoice Modal */}
            <AccessibleModal
                open={showModal}
                onClose={() => setShowModal(false)}
                title="Nueva Factura"
                maxWidth="720px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                        <button className="btn btn-primary" onClick={handleCreate} disabled={saving}>
                            {saving ? 'Creando...' : '✓ Crear Factura'}
                        </button>
                    </div>
                )}
            >

                        {formError && (
                            <div style={{ padding: '10px 12px', marginBottom: 14, borderRadius: 8, color: 'var(--danger)', background: 'var(--danger-bg)', fontSize: 13 }}>
                                {formError}
                            </div>
                        )}

                        {/* Client type toggle */}
                        <div style={{ display: 'flex', gap: '8px', marginBottom: '14px' }}>
                            {(['Registered', 'Manual'] as ClientType[]).map(t => (
                                <button key={t} type="button"
                                    onClick={() => setForm({ ...form, clientType: t, clientId: '' })}
                                    style={{
                                        padding: '6px 16px', borderRadius: '6px', fontSize: '12px', fontWeight: 600, cursor: 'pointer',
                                        border: '1px solid var(--border)',
                                        background: form.clientType === t ? 'var(--brand-primary)' : 'var(--surface)',
                                        color: form.clientType === t ? 'white' : 'var(--text-secondary)',
                                    }}>
                                    {t === 'Registered' ? 'Cliente registrado' : 'Cliente manual / B2C'}
                                </button>
                            ))}
                        </div>

                        {form.clientType === 'Registered' ? (
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                                <div className="form-group">
                                    <label className="erp-label">CLIENTE *</label>
                                    <select className="erp-input" value={form.clientId} onChange={e => setForm({ ...form, clientId: e.target.value })}>
                                        <option value="">Seleccionar cliente...</option>
                                        {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                                    </select>
                                </div>
                                <div className="form-group">
                                    <label className="erp-label">SERIE</label>
                                    <select className="erp-input" value={form.series} onChange={e => setForm({ ...form, series: e.target.value })}>
                                        <option value="A">A – General</option>
                                        <option value="S">S – Simplificada</option>
                                        <option value="R">R – Rectificativa</option>
                                        <option value="P">P – Presupuesto</option>
                                    </select>
                                </div>
                                <div className="form-group">
                                    <label className="erp-label">VENCIMIENTO</label>
                                    <input type="date" className="erp-input" value={form.dueDate} onChange={e => setForm({ ...form, dueDate: e.target.value })} />
                                </div>
                            </div>
                        ) : (
                            <div style={{ marginBottom: '16px' }}>
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                                    <div className="form-group">
                                        <label className="erp-label">NOMBRE / RAZÓN SOCIAL *</label>
                                        <input className="erp-input" placeholder="Nombre del cliente" value={form.clientName} onChange={e => setForm({ ...form, clientName: e.target.value })} />
                                    </div>
                                    <div className="form-group">
                                        <label className="erp-label">NIF / CIF</label>
                                        <input className="erp-input" placeholder="12345678A" value={form.clientTaxId} onChange={e => setForm({ ...form, clientTaxId: e.target.value })} />
                                    </div>
                                    <div className="form-group">
                                        <label className="erp-label">EMAIL</label>
                                        <input type="email" className="erp-input" placeholder="cliente@email.com" value={form.clientEmail} onChange={e => setForm({ ...form, clientEmail: e.target.value })} />
                                    </div>
                                    <div className="form-group">
                                        <label className="erp-label">DIRECCIÓN</label>
                                        <input className="erp-input" placeholder="Calle, número, ciudad" value={form.clientAddress} onChange={e => setForm({ ...form, clientAddress: e.target.value })} />
                                    </div>
                                </div>
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                                    <div className="form-group">
                                        <label className="erp-label">SERIE</label>
                                        <select className="erp-input" value={form.series} onChange={e => setForm({ ...form, series: e.target.value })}>
                                            <option value="A">A – General</option>
                                            <option value="S">S – Simplificada</option>
                                            <option value="R">R – Rectificativa</option>
                                            <option value="P">P – Presupuesto</option>
                                        </select>
                                    </div>
                                    <div className="form-group">
                                        <label className="erp-label">VENCIMIENTO</label>
                                        <input type="date" className="erp-input" value={form.dueDate} onChange={e => setForm({ ...form, dueDate: e.target.value })} />
                                    </div>
                                </div>
                            </div>
                        )}

                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group">
                                <label className="erp-label">TIPO</label>
                                <select className="erp-input" value={form.invoiceType} onChange={e => {
                                    const t = e.target.value;
                                    setForm({
                                        ...form,
                                        invoiceType: t,
                                        series: t === 'Simplificada' ? 'S' : t === 'Rectificativa' ? 'R' : form.series === 'S' || form.series === 'R' ? 'A' : form.series,
                                    });
                                }}>
                                    <option value="Normal">Normal</option>
                                    <option value="Simplificada">Simplificada (ticket, máx. 400 € base)</option>
                                    <option value="Rectificativa">Rectificativa</option>
                                </select>
                            </div>
                            <div className="form-group">
                                <label className="erp-label">IRPF PROFESIONAL (%)</label>
                                <select className="erp-input" value={form.irpfRate} onChange={e => setForm({ ...form, irpfRate: +e.target.value })}>
                                    <option value={0}>0% – No aplica</option>
                                    <option value={15}>15% – Profesionales</option>
                                    <option value={7}>7% – Inicio actividad</option>
                                </select>
                            </div>
                        </div>

                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                            <div className="form-group">
                                <label className="erp-label">DIVISA</label>
                                <select className="erp-input" value={form.currencyCode} onChange={e => setForm({ ...form, currencyCode: e.target.value })}>
                                    {currencies.map(c => (
                                        <option key={c.code} value={c.code}>{c.code} — {c.name}</option>
                                    ))}
                                </select>
                            </div>
                        </div>

                        {form.invoiceType === 'Rectificativa' && (
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px', padding: '12px', background: 'var(--surface-2)', borderRadius: '8px' }}>
                                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                                    <label className="erp-label">ID FACTURA ORIGINAL (GUID) *</label>
                                    <input className="erp-input" placeholder="UUID de la factura rectificada" value={form.rectifiedInvoiceId}
                                        onChange={e => setForm({ ...form, rectifiedInvoiceId: e.target.value })} />
                                </div>
                                <div className="form-group">
                                    <label className="erp-label">CAUSA Art. 15.1 (A–I) *</label>
                                    <select className="erp-input" value={form.rectificationReasonCode} onChange={e => setForm({ ...form, rectificationReasonCode: e.target.value })}>
                                        <option value="">Seleccionar…</option>
                                        {['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I'].map(x => <option key={x} value={x}>{x}</option>)}
                                    </select>
                                </div>
                                <div className="form-group">
                                    <label className="erp-label">Detalle (opcional)</label>
                                    <input className="erp-input" value={form.rectificationReasonText} onChange={e => setForm({ ...form, rectificationReasonText: e.target.value })} />
                                </div>
                            </div>
                        )}

                        {form.lines.some(l => l.tipoOperacion === 'IntraComunitario') && (
                            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px', fontSize: '13px', cursor: 'pointer' }}>
                                <input type="checkbox" checked={form.validateEuVatWithVies}
                                    onChange={e => setForm({ ...form, validateEuVatWithVies: e.target.checked })} />
                                Validar NIF-IVA en VIES al crear (obligatorio si marca; cliente con prefijo UE en NIF)
                            </label>
                        )}

                        {/* Lines */}
                        <div style={{ marginBottom: '16px' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                                <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Líneas de Factura</label>
                                <button className="btn btn-secondary btn-sm" onClick={addLine}>+ Añadir línea</button>
                            </div>
                            <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                                    <thead style={{ background: 'var(--surface-2)' }}>
                                        <tr>
                                            <th style={{ padding: '8px 12px', textAlign: 'left', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)' }}>DESCRIPCIÓN</th>
                                            <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '70px' }}>CANT.</th>
                                            <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '100px' }}>P. UNIT.</th>
                                            <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '80px' }}>IVA</th>
                                            <th style={{ padding: '8px 8px', textAlign: 'left', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '110px' }}>OPERACIÓN</th>
                                            <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '90px' }}>TOTAL</th>
                                            <th style={{ width: '30px', borderBottom: '1px solid var(--border)' }}></th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {form.lines.map((line, i) => (
                                            <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                                                <td style={{ padding: '6px 12px' }}>
                                                    <input id={`inv-line-desc-${i}`} className="erp-input" style={{ fontSize: '12px', padding: '5px 8px' }}
                                                        aria-label={`Descripción línea ${i + 1}`}
                                                        placeholder="Descripción del servicio o producto"
                                                        value={line.description} onChange={e => updateLine(i, 'description', e.target.value)} />
                                                </td>
                                                <td style={{ padding: '6px 8px' }}>
                                                    <input type="number" className="erp-input" style={{ fontSize: '12px', padding: '5px 8px', textAlign: 'right' }}
                                                        value={line.quantity} min="0.01" step="0.01" onChange={e => updateLine(i, 'quantity', +e.target.value)} />
                                                </td>
                                                <td style={{ padding: '6px 8px' }}>
                                                    <input type="number" className="erp-input" style={{ fontSize: '12px', padding: '5px 8px', textAlign: 'right' }}
                                                        value={line.unitPrice} min="0" step="0.01" onChange={e => updateLine(i, 'unitPrice', +e.target.value)} />
                                                </td>
                                                <td style={{ padding: '6px 8px' }}>
                                                    <select className="erp-input" style={{ fontSize: '12px', padding: '5px 8px' }}
                                                        value={line.taxRate} onChange={e => updateLine(i, 'taxRate', +e.target.value)}>
                                                        <option value={21}>21%</option>
                                                        <option value={10}>10%</option>
                                                        <option value={4}>4%</option>
                                                        <option value={0}>Exento</option>
                                                    </select>
                                                </td>
                                                <td style={{ padding: '6px 8px' }}>
                                                    <select className="erp-input" style={{ fontSize: '12px', padding: '5px 8px' }}
                                                        value={line.tipoOperacion} onChange={e => updateLine(i, 'tipoOperacion', e.target.value)}
                                                        title="Tipo de operación (Modelo 303)">
                                                        <option value="Nacional">Nacional</option>
                                                        <option value="IntraComunitario">Intracom.</option>
                                                        <option value="Exportacion">Export.</option>
                                                    </select>
                                                </td>
                                                <td style={{ padding: '6px 8px', textAlign: 'right', fontSize: '13px', fontWeight: 600 }}>
                                                    € {(line.quantity * line.unitPrice * (1 + line.taxRate / 100)).toFixed(2)}
                                                </td>
                                                <td style={{ padding: '6px 4px', textAlign: 'center' }}>
                                                    {form.lines.length > 1 && (
                                                        <button onClick={() => removeLine(i)} style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--danger)', fontSize: '16px' }}>✕</button>
                                                    )}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </div>

                        {/* Totals */}
                        <div style={{ background: 'var(--surface-2)', borderRadius: '8px', padding: '14px 16px', marginBottom: '20px' }}>
                            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '40px', fontSize: '13px' }}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', minWidth: '200px' }}>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span style={{ color: 'var(--text-secondary)' }}>Base imponible</span><span style={{ fontWeight: 600 }}>€ {subtotal.toFixed(2)}</span></div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between' }}><span style={{ color: 'var(--text-secondary)' }}>IVA total</span><span style={{ fontWeight: 600 }}>€ {taxAmount.toFixed(2)}</span></div>
                                    {form.irpfRate > 0 && <div style={{ display: 'flex', justifyContent: 'space-between' }}><span style={{ color: 'var(--text-secondary)' }}>IRPF ({form.irpfRate}%)</span><span style={{ fontWeight: 600, color: 'var(--danger)' }}>– € {irpfAmt.toFixed(2)}</span></div>}
                                    <div style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid var(--border)', paddingTop: '6px', marginTop: '2px' }}>
                                        <span style={{ fontWeight: 800, fontSize: '15px' }}>Total Factura</span>
                                        <span style={{ fontWeight: 800, fontSize: '15px', color: 'var(--brand-primary)' }}>{form.currencyCode} {total.toFixed(2)}</span>
                                    </div>
                                    {form.currencyCode !== 'EUR' && (
                                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-muted)' }}>
                                            <span>≈ en EUR (tipo Treasury)</span>
                                            <span>€ {totalEur.toFixed(2)}</span>
                                        </div>
                                    )}
                                </div>
                            </div>
                            {form.invoiceType === 'Simplificada' && subtotal > 400 && (
                                <div style={{ marginTop: '10px', fontSize: '12px', color: '#b45309', fontWeight: 600 }}>
                                    La base supera 400 €: el backend rechazará la factura simplificada (Art. 7.1 RD 1619/2012).
                                </div>
                            )}
                        </div>

            </AccessibleModal>

            {/* Chain Verification Modal */}
            <AccessibleModal
                open={showChainModal}
                onClose={() => setShowChainModal(false)}
                title="Verificación de Cadena SHA-256"
                maxWidth="420px"
                footer={(
                    <div style={{ display: 'flex', justifyContent: 'center' }}>
                        <button className="btn btn-secondary" onClick={() => setShowChainModal(false)}>Cerrar</button>
                    </div>
                )}
            >
                        {chainResult ? (
                            <div style={{ textAlign: 'center', padding: '20px 0' }}>
                                <div style={{ fontSize: '48px', marginBottom: '12px' }}>
                                    {chainResult.isValid ? '✅' : '❌'}
                                </div>
                                <div style={{ fontSize: '18px', fontWeight: 800, color: chainResult.isValid ? 'var(--success)' : 'var(--danger)', marginBottom: '8px' }}>
                                    {chainResult.isValid ? 'Cadena íntegra' : 'Error de integridad'}
                                </div>
                                <div style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '16px' }}>
                                    {chainResult.totalVerified} facturas verificadas (Serie A · {new Date().getFullYear()})
                                </div>
                                {chainResult.errorAt && (
                                    <div style={{ background: 'var(--danger-bg)', color: 'var(--danger)', padding: '10px 14px', borderRadius: '8px', fontSize: '12px', fontFamily: 'monospace' }}>
                                        Error en factura: {chainResult.errorAt}
                                    </div>
                                )}
                                {chainResult.isValid && (
                                    <div style={{ background: 'var(--success-bg)', color: 'var(--success)', padding: '10px 14px', borderRadius: '8px', fontSize: '12px' }}>
                                        La cadena de hashes SHA-256 es correcta. Cumplimiento Ley 11/2021 verificado.
                                    </div>
                                )}
                            </div>
                        ) : (
                            <div style={{ textAlign: 'center', padding: '20px', color: 'var(--text-muted)' }}>Error al contactar con el servidor</div>
                        )}
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
