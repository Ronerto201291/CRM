'use client';
import { useMemo, useState } from 'react';
import Link from 'next/link';
import PageContainer from '@/components/PageContainer';
import { parseListResponse } from '@/lib/parseListResponse';
import { useCachedApi } from '@/hooks/useCachedApi';

export interface PurchaseOrder {
    id: string;
    number: string;
    orderDate: string;
    status: string;
    totalAmount: number;
    lines?: { productId?: string; quantity: number; unitPrice: number }[];
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Draft: { label: 'Borrador', cls: 'badge-gray' },
    PendingApproval: { label: 'Pendiente aprobación', cls: 'badge-warning' },
    Approved: { label: 'Aprobado', cls: 'badge-success' },
    Rejected: { label: 'Rechazado', cls: 'badge-danger' },
};

interface PurchasingOrdersClientProps {
    initialOrders: PurchaseOrder[];
}

export default function PurchasingOrdersClient({ initialOrders }: PurchasingOrdersClientProps) {
    const [orders, setOrders] = useState<PurchaseOrder[]>(initialOrders);
    const [loading, setLoading] = useState(false);
    const [filter, setFilter] = useState('all');
    const { fetchCached, invalidateCached } = useCachedApi();

    const refresh = async () => {
        setLoading(true);
        try {
            invalidateCached('v1/purchasing/orders');
            const data = await fetchCached<unknown>('v1/purchasing/orders');
            if (data) setOrders(parseListResponse<PurchaseOrder>(data));
        } finally {
            setLoading(false);
        }
    };

    const filtered = useMemo(
        () => (filter === 'all' ? orders : orders.filter(o => o.status === filter)),
        [orders, filter]
    );
    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    const filters = ['all', 'Draft', 'PendingApproval', 'Approved', 'Rejected'];

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Pedidos de Compra</h1>
                    <p className="page-subtitle">Gestión de pedidos a proveedores con aprobación por umbral</p>
                </div>
                <div style={{ display: 'flex', gap: '8px' }}>
                    <button className="btn btn-secondary" onClick={refresh} disabled={loading}>Actualizar</button>
                    <Link href="/purchasing/orders/new" className="btn btn-primary">+ Nuevo Pedido</Link>
                </div>
            </div>

            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px', flexWrap: 'wrap' }}>
                {filters.map(f => (
                    <button key={f} onClick={() => setFilter(f)}
                        style={{
                            padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)',
                            fontSize: '12px', fontWeight: filter === f ? 700 : 500,
                            background: filter === f ? 'var(--brand-primary)' : 'var(--surface)',
                            color: filter === f ? 'white' : 'var(--text-secondary)',
                            cursor: 'pointer',
                        }}>
                        {f === 'all' ? 'Todos' : STATUS_MAP[f]?.label ?? f}
                    </button>
                ))}
            </div>

            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Fecha</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                            <th>Estado</th>
                            <th>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>
                        {loading && <tr><td colSpan={5} style={{ textAlign: 'center', padding: '20px', color: 'var(--text-muted)' }}>Cargando...</td></tr>}
                        {!loading && filtered.length === 0 && (
                            <tr><td colSpan={5}>
                                <div className="empty-state">
                                    <div className="empty-state-icon">🛒</div>
                                    <div className="empty-state-title">Sin pedidos de compra</div>
                                </div>
                            </td></tr>
                        )}
                        {filtered.map(o => (
                            <tr key={o.id}>
                                <td style={{ fontWeight: 700, fontFamily: 'monospace', fontSize: '12px' }}>
                                    <Link href={`/purchasing/orders/${o.id}`} style={{ color: 'var(--brand-primary)' }}>{o.number}</Link>
                                </td>
                                <td style={{ color: 'var(--text-secondary)' }}>{new Date(o.orderDate).toLocaleDateString('es-ES')}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(o.totalAmount)}</td>
                                <td><span className={`badge ${STATUS_MAP[o.status]?.cls ?? 'badge-gray'}`}>{STATUS_MAP[o.status]?.label ?? o.status}</span></td>
                                <td>
                                    <Link href={`/purchasing/orders/${o.id}`} className="btn btn-secondary" style={{ padding: '4px 10px', fontSize: '12px' }}>Ver</Link>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </PageContainer>
    );
}
