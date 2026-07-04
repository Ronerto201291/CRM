'use client';
import { useState, useCallback, useMemo } from 'react';
import PageContainer from '@/components/PageContainer';
import { useCachedApi } from '@/hooks/useCachedApi';
import { parseListResponse } from '@/lib/parseListResponse';

interface SupplierInvoice {
    id: string;
    number: string;
    invoiceDate: string;
    supplierId?: string;
    supplierName?: string;
    supplierTaxId?: string;
    subtotal: number;
    taxAmount: number;
    total: number;
    status: 'Draft' | 'Approved' | 'Paid';
    createdAt: string;
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Draft: { label: 'Borrador', cls: 'badge-gray' },
    Approved: { label: 'Aprobada', cls: 'badge-info' },
    Paid: { label: 'Pagada', cls: 'badge-success' },
};

interface PurchasingInvoicesClientProps {
    initialInvoices: SupplierInvoice[];
}

export default function PurchasingInvoicesClient({ initialInvoices }: PurchasingInvoicesClientProps) {
    const [invoices, setInvoices] = useState<SupplierInvoice[]>(initialInvoices);
    const [loading, setLoading] = useState(false);
    const [filter, setFilter] = useState('all');
    const { fetchCached, invalidateCached } = useCachedApi();

    const load = useCallback(async () => {
        setLoading(true);
        try {
            invalidateCached('purchasing/invoices');
            const data = await fetchCached<unknown>('purchasing/invoices');
            if (data) setInvoices(parseListResponse<SupplierInvoice>(data));
        } finally {
            setLoading(false);
        }
    }, [fetchCached, invalidateCached]);

    const filtered = useMemo(
        () => (filter === 'all' ? invoices : invoices.filter(i => i.status === filter)),
        [invoices, filter]
    );
    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Facturas de Proveedor</h1>
                    <p className="page-subtitle">Gestión de facturas de compra</p>
                </div>
                <a href="/purchasing/invoices/new" className="btn btn-primary">
                    + Nueva Factura
                </a>
            </div>

            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px' }}>
                {['all', 'Draft', 'Approved', 'Paid'].map(f => (
                    <button key={f} onClick={() => setFilter(f)}
                        style={{
                            padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)',
                            fontSize: '12px', fontWeight: filter === f ? 700 : 500,
                            background: filter === f ? 'var(--brand-primary)' : 'var(--surface)',
                            color: filter === f ? 'white' : 'var(--text-secondary)',
                            cursor: 'pointer',
                        }}>
                        {f === 'all' ? 'Todas' : STATUS_MAP[f]?.label ?? f}
                        {f !== 'all' && (
                            <span style={{ marginLeft: '6px', opacity: 0.7 }}>
                                ({invoices.filter(i => i.status === f).length})
                            </span>
                        )}
                    </button>
                ))}
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '14px', marginBottom: '20px' }}>
                <MiniStat label="Total" value={fmt(invoices.reduce((s, i) => s + i.total, 0))} color="var(--brand-primary)" />
                <MiniStat label="Borrador" value={fmt(invoices.filter(i => i.status === 'Draft').reduce((s, i) => s + i.total, 0))} color="var(--text-muted)" />
                <MiniStat label="Aprobadas" value={fmt(invoices.filter(i => i.status === 'Approved').reduce((s, i) => s + i.total, 0))} color="var(--warning)" />
                <MiniStat label="Pagadas" value={fmt(invoices.filter(i => i.status === 'Paid').reduce((s, i) => s + i.total, 0))} color="var(--success)" />
            </div>

            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Proveedor</th>
                            <th>Fecha</th>
                            <th style={{ textAlign: 'right' }}>Base</th>
                            <th style={{ textAlign: 'right' }}>IVA</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                            <th>Estado</th>
                        </tr>
                    </thead>
                    <tbody>
                        {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: '20px', color: 'var(--text-muted)' }}>Cargando...</td></tr>}
                        {!loading && filtered.length === 0 && (
                            <tr><td colSpan={7}>
                                <div className="empty-state">
                                    <div className="empty-state-icon">📄</div>
                                    <div className="empty-state-title">Sin facturas de proveedor</div>
                                    <div className="empty-state-sub">Crea tu primera factura desde el botón superior</div>
                                </div>
                            </td></tr>
                        )}
                        {filtered.map(inv => (
                            <tr key={inv.id}>
                                <td style={{ fontWeight: 700, fontFamily: 'monospace', fontSize: '12px' }}>{inv.number}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{inv.supplierName || '—'}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{new Date(inv.invoiceDate).toLocaleDateString('es-ES')}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(inv.subtotal)}</td>
                                <td style={{ textAlign: 'right', color: 'var(--text-secondary)' }}>{fmt(inv.taxAmount)}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(inv.total)}</td>
                                <td><span className={`badge ${STATUS_MAP[inv.status]?.cls ?? 'badge-gray'}`}>{STATUS_MAP[inv.status]?.label ?? inv.status}</span></td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
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
