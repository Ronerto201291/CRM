'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import PageContainer from '@/components/PageContainer';
import { parseListResponse } from '@/lib/parseListResponse';
import { useCachedApi } from '@/hooks/useCachedApi';

export interface SalesOrder {
    id: string;
    number: string;
    orderDate: string;
    customerName: string;
    status: string;
    total: number;
    lineCount: number;
    createdAt: string;
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Open: { label: 'Abierto', cls: 'badge-info' },
    Shipped: { label: 'Enviado', cls: 'badge-warning' },
    Delivered: { label: 'Entregado', cls: 'badge-success' },
    Cancelled: { label: 'Cancelado', cls: 'badge-gray' },
};

interface SalesOrdersClientProps {
    initialOrders: SalesOrder[];
}

export default function SalesOrdersClient({ initialOrders }: SalesOrdersClientProps) {
    const [orders, setOrders] = useState<SalesOrder[]>(initialOrders);
    const [loading, setLoading] = useState(false);
    const [filter, setFilter] = useState('all');
    const { fetchCached, invalidateCached } = useCachedApi();

    const refresh = async () => {
        setLoading(true);
        try {
            invalidateCached('v1/sales/orders');
            const data = await fetchCached<unknown>('v1/sales/orders');
            if (data) setOrders(parseListResponse<SalesOrder>(data));
        } finally {
            setLoading(false);
        }
    };

    const filtered = useMemo(
        () => (filter === 'all' ? orders : orders.filter(o => o.status === filter)),
        [orders, filter]
    );
    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Pedidos de Venta</h1>
                    <p className="page-subtitle">Gestión de pedidos a clientes</p>
                </div>
                <div style={{ display: 'flex', gap: '8px' }}>
                    <button className="btn btn-secondary" onClick={refresh} disabled={loading}>Actualizar</button>
                    <Link href="/sales/orders/new" className="btn btn-primary">+ Nuevo Pedido</Link>
                </div>
            </div>

            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px' }}>
                {['all', 'Open', 'Shipped', 'Delivered', 'Cancelled'].map(f => (
                    <button key={f} onClick={() => setFilter(f)}
                        style={{
                            padding: '6px 14px', borderRadius: '6px', border: '1px solid var(--border)',
                            fontSize: '12px', fontWeight: filter === f ? 700 : 500,
                            background: filter === f ? 'var(--brand-primary)' : 'var(--surface)',
                            color: filter === f ? 'white' : 'var(--text-secondary)',
                            cursor: 'pointer',
                        }}>
                        {f === 'all' ? 'Todos' : STATUS_MAP[f]?.label ?? f}
                        {f !== 'all' && (
                            <span style={{ marginLeft: '6px', opacity: 0.7 }}>
                                ({orders.filter(o => o.status === f).length})
                            </span>
                        )}
                    </button>
                ))}
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '14px', marginBottom: '20px' }}>
                <MiniStat label="Total Pedidos" value={fmt(orders.reduce((s, o) => s + o.total, 0))} color="var(--brand-primary)" />
                <MiniStat label="Abiertos" value={fmt(orders.filter(o => o.status === 'Open').reduce((s, o) => s + o.total, 0))} color="var(--info)" />
                <MiniStat label="Enviados" value={fmt(orders.filter(o => o.status === 'Shipped').reduce((s, o) => s + o.total, 0))} color="var(--warning)" />
                <MiniStat label="Entregados" value={fmt(orders.filter(o => o.status === 'Delivered').reduce((s, o) => s + o.total, 0))} color="var(--success)" />
            </div>

            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Cliente</th>
                            <th>Fecha</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                            <th>Estado</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody>
                        {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: '20px', color: 'var(--text-muted)' }}>Cargando...</td></tr>}
                        {!loading && filtered.length === 0 && (
                            <tr><td colSpan={6}>
                                <div className="empty-state">
                                    <div className="empty-state-icon">📦</div>
                                    <div className="empty-state-title">Sin pedidos de venta</div>
                                    <div className="empty-state-sub">Crea tu primer pedido de venta</div>
                                </div>
                            </td></tr>
                        )}
                        {filtered.map(o => (
                            <tr key={o.id}>
                                <td style={{ fontWeight: 700, fontFamily: 'monospace', fontSize: '12px', color: 'var(--brand-primary)' }}>{o.number}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{o.customerName || '—'}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{new Date(o.orderDate).toLocaleDateString('es-ES')}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(o.total)}</td>
                                <td><span className={`badge ${STATUS_MAP[o.status]?.cls ?? 'badge-gray'}`}>{STATUS_MAP[o.status]?.label ?? o.status}</span></td>
                                <td>
                                    <a href={`/sales/orders/${o.id}`} className="btn btn-secondary btn-sm">Ver</a>
                                </td>
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
