"use client";

import React, { useEffect, useState, useCallback } from "react";
import PageContainer from "@/components/PageContainer";

interface DeliveryNote {
    id: string;
    number: string;
    deliveryDate: string;
    customerName: string;
    status: string;
    lineCount: number;
    createdAt: string;
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Pending: { label: 'Pendiente', cls: 'badge-info' },
    Shipped: { label: 'Enviado', cls: 'badge-warning' },
    Delivered: { label: 'Entregado', cls: 'badge-success' },
    Cancelled: { label: 'Cancelado', cls: 'badge-gray' },
};

export default function SalesDeliveriesPage() {
    const [deliveries, setDeliveries] = useState<DeliveryNote[]>([]);
    const [loading, setLoading] = useState(false);
    const [filter, setFilter] = useState('all');

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const res = await fetch('/api/proxy/v1/sales/deliveries');
            if (res.ok) {
                const data = await res.json();
                setDeliveries(Array.isArray(data) ? data : (data.items ?? []));
            }
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => { load(); }, [load]);

    const filtered = filter === 'all' ? deliveries : deliveries.filter(d => d.status === filter);

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Albaranes de Entrega</h1>
                    <p className="page-subtitle">Gestión de entregas a clientes</p>
                </div>
                <a href="/sales/deliveries/new" className="btn btn-primary">
                    + Nuevo Albarán
                </a>
            </div>

            <div style={{ display: 'flex', gap: '6px', marginBottom: '20px' }}>
                {['all', 'Pending', 'Shipped', 'Delivered', 'Cancelled'].map(f => (
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
                                ({deliveries.filter(d => d.status === f).length})
                            </span>
                        )}
                    </button>
                ))}
            </div>

            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Cliente</th>
                            <th>Fecha Entrega</th>
                            <th>Estado</th>
                            <th>Líneas</th>
                        </tr>
                    </thead>
                    <tbody>
                        {loading && <tr><td colSpan={5} style={{ textAlign: 'center', padding: '20px', color: 'var(--text-muted)' }}>Cargando...</td></tr>}
                        {!loading && filtered.length === 0 && (
                            <tr><td colSpan={5}>
                                <div className="empty-state">
                                    <div className="empty-state-icon">🚚</div>
                                    <div className="empty-state-title">Sin albaranes de entrega</div>
                                    <div className="empty-state-sub">Crea tu primer albarán desde el botón superior</div>
                                </div>
                            </td></tr>
                        )}
                        {filtered.map(d => (
                            <tr key={d.id}>
                                <td style={{ fontWeight: 700, fontFamily: 'monospace', fontSize: '12px', color: 'var(--brand-primary)' }}>{d.number}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{d.customerName || '—'}</td>
                                <td style={{ color: 'var(--text-secondary)' }}>{new Date(d.deliveryDate).toLocaleDateString('es-ES')}</td>
                                <td><span className={`badge ${STATUS_MAP[d.status]?.cls ?? 'badge-gray'}`}>{STATUS_MAP[d.status]?.label ?? d.status}</span></td>
                                <td style={{ color: 'var(--text-muted)', fontSize: '12px' }}>{d.lineCount}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </PageContainer>
    );
}