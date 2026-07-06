"use client";

import React, { useState, useCallback } from "react";
import Link from "next/link";
import PageContainer from "@/components/PageContainer";

interface OrderLine {
    id: string;
    productId?: string;
    description: string;
    quantity: number;
    unitPrice: number;
    taxRate: number;
    total: number;
}

interface SalesOrderDetail {
    id: string;
    number: string;
    orderDate: string;
    customerName: string;
    customerTaxId?: string;
    customerEmail?: string;
    status: string;
    subtotal: number;
    taxAmount: number;
    total: number;
    notes?: string;
    lines: OrderLine[];
}

import { SALES_ORDER_STATUS_MAP } from '@/lib/salesOrderStatus';

interface OrderDetailClientProps {
    id: string;
    initialOrder: SalesOrderDetail | null;
}

export default function OrderDetailClient({ id, initialOrder }: OrderDetailClientProps) {
    const [order, setOrder] = useState<SalesOrderDetail | null>(initialOrder);
    const [loading, setLoading] = useState(false);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const res = await fetch(`/api/proxy/v1/sales/orders/${id}`);
            if (res.ok) setOrder(await res.json());
            else setOrder(null);
        } finally {
            setLoading(false);
        }
    }, [id]);

    const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    if (loading) return <PageContainer><div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>Cargando...</div></PageContainer>;
    if (!order) return <PageContainer><div style={{ padding: '40px', textAlign: 'center' }}>Pedido no encontrado</div></PageContainer>;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Pedido {order.number}</h1>
                    <p className="page-subtitle">{order.customerName}</p>
                </div>
                <Link href="/sales/orders" className="btn btn-secondary">← Volver</Link>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '20px', marginBottom: '20px' }}>
                <div className="erp-card">
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '12px' }}>Información del Cliente</div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                        <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Nombre</span><div style={{ fontWeight: 600 }}>{order.customerName}</div></div>
                        {order.customerTaxId && <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>NIF/CIF</span><div style={{ fontWeight: 600 }}>{order.customerTaxId}</div></div>}
                        {order.customerEmail && <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Email</span><div style={{ fontWeight: 600 }}>{order.customerEmail}</div></div>}
                        <div><span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Fecha</span><div style={{ fontWeight: 600 }}>{new Date(order.orderDate).toLocaleDateString('es-ES')}</div></div>
                    </div>
                </div>
                <div className="erp-card" style={{ textAlign: 'center' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '8px' }}>Estado</div>
                    <span className={`badge ${SALES_ORDER_STATUS_MAP[order.status]?.cls ?? 'badge-gray'}`} style={{ fontSize: '14px', padding: '8px 16px' }}>
                        {SALES_ORDER_STATUS_MAP[order.status]?.label ?? order.status}
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
                            <th style={{ textAlign: 'right' }}>Cantidad</th>
                            <th style={{ textAlign: 'right' }}>P. Unit.</th>
                            <th style={{ textAlign: 'right' }}>IVA</th>
                            <th style={{ textAlign: 'right' }}>Total</th>
                        </tr>
                    </thead>
                    <tbody>
                        {order.lines.map(line => (
                            <tr key={line.id}>
                                <td style={{ fontWeight: 500 }}>{line.description}</td>
                                <td style={{ textAlign: 'right' }}>{line.quantity}</td>
                                <td style={{ textAlign: 'right' }}>{fmt(line.unitPrice)}</td>
                                <td style={{ textAlign: 'right' }}>{line.taxRate}%</td>
                                <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(line.total)}</td>
                            </tr>
                        ))}
                    </tbody>
                    <tfoot style={{ background: 'var(--surface-2)' }}>
                        <tr>
                            <td colSpan={4} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 600 }}>Base imponible</td>
                            <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(order.subtotal)}</td>
                        </tr>
                        <tr>
                            <td colSpan={4} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 600 }}>IVA</td>
                            <td style={{ textAlign: 'right', fontWeight: 700 }}>{fmt(order.taxAmount)}</td>
                        </tr>
                        <tr>
                            <td colSpan={4} style={{ textAlign: 'right', padding: '8px 12px', fontWeight: 800, fontSize: '15px' }}>Total</td>
                            <td style={{ textAlign: 'right', fontWeight: 800, fontSize: '15px', color: 'var(--brand-primary)' }}>{fmt(order.total)}</td>
                        </tr>
                    </tfoot>
                </table>
            </div>

            <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                {(order.status === 'Open' || order.status === 'PartiallyDelivered') && (
                    <Link href={`/sales/deliveries/new?salesOrderId=${order.id}`} className="btn btn-primary">
                        + Crear albarán de entrega
                    </Link>
                )}
                {order.status === 'Completed' && (
                    <Link href={`/sales/invoices/new?salesOrderId=${order.id}`} className="btn btn-secondary">
                        Facturar pedido
                    </Link>
                )}
            </div>
        </PageContainer>
    );
}