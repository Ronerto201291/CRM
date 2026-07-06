'use client';

import { useState, useCallback } from 'react';
import Link from 'next/link';
import PageContainer from '@/components/PageContainer';

export interface PurchaseOrderDetail {
    id: string;
    number: string;
    orderDate: string;
    supplierId?: string;
    supplierName?: string;
    status: string;
    totalAmount: number;
    lines: { productId?: string; quantity: number; unitPrice: number }[];
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Draft: { label: 'Borrador', cls: 'badge-gray' },
    PendingApproval: { label: 'Pendiente aprobación', cls: 'badge-warning' },
    Approved: { label: 'Aprobado', cls: 'badge-success' },
    Rejected: { label: 'Rechazado', cls: 'badge-danger' },
};

interface OrderDetailClientProps {
    id: string;
    initialOrder: PurchaseOrderDetail | null;
}

export default function OrderDetailClient({ id, initialOrder }: OrderDetailClientProps) {
    const [order, setOrder] = useState<PurchaseOrderDetail | null>(initialOrder);
    const [actionLoading, setActionLoading] = useState<string | null>(null);
    const [actionError, setActionError] = useState<string | null>(null);
    const [successMsg, setSuccessMsg] = useState<string | null>(null);

    const load = useCallback(async () => {
        const res = await fetch(`/api/proxy/v1/purchasing/orders/${id}`);
        if (res.ok) setOrder(await res.json());
        else setOrder(null);
    }, [id]);

    const doPost = async (action: string) => {
        setActionLoading(action);
        setActionError(null);
        setSuccessMsg(null);
        try {
            const res = await fetch(`/api/proxy/v1/purchasing/orders/${id}/${action}`, { method: 'POST' });
            if (res.ok) {
                setSuccessMsg('Acción realizada correctamente');
                await load();
            } else {
                const e = await res.json();
                setActionError(e.error || 'Error');
            }
        } finally {
            setActionLoading(null);
        }
    };

    const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    if (!order) return <PageContainer><div style={{ padding: '40px', textAlign: 'center' }}>Pedido no encontrado</div></PageContainer>;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Pedido {order.number}</h1>
                    <p className="page-subtitle">Total {fmt(order.totalAmount)}</p>
                </div>
                <Link href="/purchasing/orders" className="btn btn-secondary">← Volver</Link>
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

            <div className="erp-card" style={{ marginBottom: '20px', textAlign: 'center' }}>
                <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '8px' }}>Estado</div>
                <span className={`badge ${STATUS_MAP[order.status]?.cls ?? 'badge-gray'}`} style={{ fontSize: '14px', padding: '8px 16px' }}>
                    {STATUS_MAP[order.status]?.label ?? order.status}
                </span>
                <div style={{ marginTop: '12px', color: 'var(--text-secondary)', fontSize: '13px' }}>
                    Proveedor: {order.supplierName || '—'}
                </div>
                <div style={{ marginTop: '6px', color: 'var(--text-secondary)', fontSize: '13px' }}>
                    Fecha: {new Date(order.orderDate).toLocaleDateString('es-ES')}
                </div>
            </div>

            <div className="erp-card" style={{ marginBottom: '20px' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th style={{ textAlign: 'right' }}>Cant.</th>
                            <th style={{ textAlign: 'right' }}>P. Unit.</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                        </tr>
                    </thead>
                    <tbody>
                        {order.lines.map((line, i) => (
                            <tr key={i}>
                                <td style={{ textAlign: 'right' }}>{line.quantity}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(line.unitPrice)}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(line.quantity * line.unitPrice)}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                {(order.status === 'Draft' || order.status === 'Rejected') && (
                    <button className="btn btn-primary" onClick={() => doPost('submit-for-approval')} disabled={!!actionLoading}>
                        {actionLoading === 'submit-for-approval' ? 'Enviando...' : 'Enviar a aprobación'}
                    </button>
                )}
                {order.status === 'PendingApproval' && (
                    <>
                        <button className="btn btn-primary" onClick={() => doPost('approve')} disabled={!!actionLoading}>
                            {actionLoading === 'approve' ? 'Aprobando...' : '✓ Aprobar'}
                        </button>
                        <button className="btn btn-secondary" onClick={() => doPost('reject')} disabled={!!actionLoading} style={{ color: 'var(--danger)' }}>
                            {actionLoading === 'reject' ? 'Rechazando...' : '✕ Rechazar'}
                        </button>
                    </>
                )}
                {order.status === 'Approved' && (
                    <>
                        <a href={`/purchasing/receipts/new?purchaseOrderId=${order.id}`} className="btn btn-secondary">📦 Registrar recepción</a>
                        <a href={`/purchasing/invoices/new?purchaseOrderId=${order.id}`} className="btn btn-secondary">📄 Crear factura</a>
                    </>
                )}
            </div>
        </PageContainer>
    );
}
