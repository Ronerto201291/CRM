"use client";

import React, { useEffect, useState, useCallback } from "react";
import { useParams } from "next/navigation";
import PageContainer from "@/components/PageContainer";

interface OrderLine {
    id: string;
    productId?: string;
    description: string;
    quantity: number;
    deliveredQuantity: number;
    billedQuantity: number;
    unitPrice: number;
    taxRate: number;
    total: number;
}

interface PurchaseOrderDetail {
    id: string;
    number: string;
    orderDate: string;
    supplierName: string;
    supplierTaxId?: string;
    supplierEmail?: string;
    status: string;
    subtotal: number;
    taxAmount: number;
    total: number;
    notes?: string;
    lines: OrderLine[];
}

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
    Open: { label: 'Abierto', cls: 'badge-info' },
    PartiallyReceived: { label: 'Parcial', cls: 'badge-warning' },
    Completed: { label: 'Completado', cls: 'badge-success' },
    Cancelled: { label: 'Cancelado', cls: 'badge-gray' },
};

export default function PurchaseOrderDetailPage() {
    const { id } = useParams<{ id: string }>();
    const [order, setOrder] = useState<PurchaseOrderDetail | null>(null);
    const [loading, setLoading] = useState(true);
    const [actionLoading, setActionLoading] = useState<string | null>(null);
    const [actionError, setActionError] = useState<string | null>(null);
    const [successMsg, setSuccessMsg] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const res = await fetch(`/api/proxy/v1/purchasing/orders/${id}`);
            if (res.ok) setOrder(await res.json());
            else setOrder(null);
        } finally {
            setLoading(false);
        }
    }, [id]);

    useEffect(() => { load(); }, [load]);

    const doAction = async (action: string) => {
        setActionLoading(action);
        setActionError(null);
        setSuccessMsg(null);
        try {
            const res = await fetch(`/api/proxy/v1/purchasing/orders/${id}/${action}`, { method: 'PATCH' });
            if (res.ok) { setSuccessMsg('Acción realizada'); load(); }
            else { const e = await res.json(); setActionError(e.error || 'Error'); }
        } finally {
            setActionLoading(null);
        }
    };

    const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    if (loading) return <PageContainer><div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>Cargando...</div></PageContainer>;
    if (!order) return <PageContainer><div style={{ padding: '40px', textAlign: 'center' }}>Pedido no encontrado</div></PageContainer>;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Pedido {order.number}</h1>
                    <p className="page-subtitle">{order.supplierName}</p>
                </div>
                <a href="/purchasing/orders" className="btn btn-secondary">← Volver</a>
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

            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '20px', marginBottom: '20px' }}>
                <div className="erp-card">
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '12px' }}>Información del Proveedor</div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                        <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Nombre</span><div style={{ fontWeight: 600 }}>{order.supplierName}</div></div>
                        {order.supplierTaxId && <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>NIF/CIF</span><div style={{ fontWeight: 600 }}>{order.supplierTaxId}</div></div>}
                        {order.supplierEmail && <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Email</span><div style={{ fontWeight: 600 }}>{order.supplierEmail}</div></div>}
                        <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Fecha</span><div style={{ fontWeight: 600 }}>{new Date(order.orderDate).toLocaleDateString('es-ES')}</div></div>
                    </div>
                </div>
                <div className="erp-card" style={{ textAlign: 'center' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '8px' }}>Estado</div>
                    <span className={`badge ${STATUS_MAP[order.status]?.cls ?? 'badge-gray'}`} style={{ fontSize: '14px', padding: '8px 16px' }}>
                        {STATUS_MAP[order.status]?.label ?? order.status}
                    </span>
                    <div style={{ marginTop: '16px', fontSize: '24px', fontWeight: 800, color: 'var(--brand-primary)' }}>{fmt(order.total)}</div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Total pedido</div>
                </div>
            </div>

            {order.notes && (
                <div className="erp-card" style={{ marginBottom: '20px' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '6px' }}>Notas</div>
                    <div style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>{order.notes}</div>
                </div>
            )}

            <div className="erp-card" style={{ marginBottom: '20px' }}>
                <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '12px' }}>Líneas del Pedido</div>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Descripción</th>
                            <th style={{ textAlign: 'right' }}>Cant.</th>
                            <th style={{ textAlign: 'right' }}>Recibido</th>
                            <th style={{ textAlign: 'right' }}>Facturado</th>
                            <th style={{ textAlign: 'right' }}>P. Unit.</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                        </tr>
                    </thead>
                    <tbody>
                        {order.lines.map(line => (
                            <tr key={line.id}>
                                <td style={{ fontWeight: 500 }}>{line.description}</td>
                                <td style={{ textAlign: 'right' }}>{line.quantity}</td>
                                <td style={{ textAlign: 'right', color: line.deliveredQuantity >= line.quantity ? 'var(--success)' : 'var(--warning)' }}>{line.deliveredQuantity}</td>
                                <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>{line.billedQuantity}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(line.unitPrice)}</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(line.total)}</td>
                            </tr>
                        ))}
                    </tbody>
                    <tfoot style={{ background: 'var(--surface-2)' }}>
                        <tr>
                            <td colSpan={5} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 600 }}>Base imponible</td>
                            <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(order.subtotal)}</td>
                        </tr>
                        <tr>
                            <td colSpan={5} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 600 }}>IVA</td>
                            <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(order.taxAmount)}</td>
                        </tr>
                        <tr>
                            <td colSpan={5} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 800, fontSize: '15px' }}>Total</td>
                            <td style={{ textAlign: 'right', fontWeight: 800, fontSize: '15px', color: 'var(--brand-primary)' }}>{fmt(order.total)}</td>
                        </tr>
                    </tfoot>
                </table>
            </div>

            <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                {order.status === 'Open' && (
                    <>
                        <a href={`/purchasing/receipts/new?purchaseOrderId=${order.id}`} className="btn btn-secondary">
                            📦 Registrar Recepción
                        </a>
                        <a href={`/purchasing/invoices/new?purchaseOrderId=${order.id}`} className="btn btn-secondary">
                            📄 Crear Factura
                        </a>
                        <button className="btn btn-secondary" onClick={() => doAction('cancel')} disabled={!!actionLoading} style={{ color: 'var(--danger)' }}>
                            {actionLoading === 'cancel' ? 'Cancelando...' : '✕ Cancelar'}
                        </button>
                    </>
                )}
                {order.status === 'PartiallyReceived' && (
                    <>
                        <a href={`/purchasing/receipts/new?purchaseOrderId=${order.id}`} className="btn btn-secondary">
                            📦 Registrar Recepción
                        </a>
                        <a href={`/purchasing/invoices/new?purchaseOrderId=${order.id}`} className="btn btn-secondary">
                            📄 Crear Factura
                        </a>
                    </>
                )}
            </div>
        </PageContainer>
    );
}