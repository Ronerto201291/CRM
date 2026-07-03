'use client';
import { useState } from 'react';
import Link from 'next/link';
import PageContainer from '@/components/PageContainer';
import { parseListResponse } from '@/lib/parseListResponse';

export interface PurchaseOrder {
    id: string;
    number: string;
    orderDate: string;
    supplierName: string;
    status: 'Open' | 'PartiallyReceived' | 'Completed' | 'Cancelled';
    subtotal: number;
    taxAmount: number;
    total: number;
    createdAt: string;
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Open: { label: 'Abierto', cls: 'badge-info' },
    PartiallyReceived: { label: 'Parcial', cls: 'badge-warning' },
    Completed: { label: 'Completado', cls: 'badge-success' },
    Cancelled: { label: 'Cancelado', cls: 'badge-gray' },
};

interface PurchasingOrdersClientProps {
    initialOrders: PurchaseOrder[];
}

export default function PurchasingOrdersClient({ initialOrders }: PurchasingOrdersClientProps) {
    const [orders, setOrders] = useState<PurchaseOrder[]>(initialOrders);
    const [loading, setLoading] = useState(false);
    const [filter, setFilter] = useState('all');

    const refresh = async () => {
        setLoading(true);
        try {
            const res = await fetch('/api/proxy/purchasing/orders');
            if (res.ok) setOrders(parseListResponse<PurchaseOrder>(await res.json()));
        } finally {
            setLoading(false);
        }
    };

    const filtered = filter === 'all' ? orders : orders.filter(o => o.status === filter);
    const fmt = (n: number) => `€ ${(n || 0).toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Pedidos de Compra</h1>
                    <p className="page-subtitle">Gestión de pedidos a proveedores</p>
                </div>
                <div style={{ display: 'flex', gap: '8px' }}>
                    <button className="btn btn-secondary" onClick={refresh} disabled={loading}>Actualizar</button>
                    <Link href="/purchasing/orders/new" className="btn btn-primary">+ Nuevo Pedido</Link>
                </div>
            </div>

            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px' }}>
                {['all', 'Open', 'PartiallyReceived', 'Completed', 'Cancelled'].map(f => (
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
                <MiniStat label="Parcialmente Recibidos" value={fmt(orders.filter(o => o.status === 'PartiallyReceived').reduce((s, o) => s + o.total, 0))} color="var(--warning)" />
                <MiniStat label="Completados" value={fmt(orders.filter(o => o.status === 'Completed').reduce((s, o) => s + o.total, 0))} color="var(--success)" />
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
                                    <div className="empty-state-icon">🛒</div>
                                    <div className="empty-state-title">Sin pedidos de compra</div>
                                    <div className="empty-state-sub">Crea tu primer pedido desde el botón superior</div>
                                </div>
                            </td></tr>
                        )}
                        {filtered.map(o => (
                            <tr key={o.id}>
                                <td style={{ fontWeight: 700, fontFamily: 'monospace', fontSize: '12px', color: 'var(--brand-primary)' }}>{o.number}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{o.supplierName || '—'}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{new Date(o.orderDate).toLocaleDateString('es-ES')}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(o.subtotal)}</td>
                                <td style={{ textAlign: 'right', color: 'var(--text-secondary)' }}>{fmt(o.taxAmount)}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(o.total)}</td>
                                <td><span className={`badge ${STATUS_MAP[o.status]?.cls ?? 'badge-gray'}`}>{STATUS_MAP[o.status]?.label ?? o.status}</span></td>
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
