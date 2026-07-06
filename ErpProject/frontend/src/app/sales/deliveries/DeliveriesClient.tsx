"use client";

import React from "react";
import Link from "next/link";
import PageContainer from "@/components/PageContainer";

interface DeliveryNote {
    id: string;
    number: string;
    deliveryDate: string;
    lineCount: number;
    salesOrderId?: string;
    createdAt: string;
}

interface DeliveriesClientProps {
    initialDeliveries: DeliveryNote[];
}

export default function DeliveriesClient({ initialDeliveries }: DeliveriesClientProps) {
    const deliveries = initialDeliveries;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Albaranes de entrega</h1>
                    <p className="page-subtitle">Entregas parciales o totales vinculadas a pedidos de venta</p>
                </div>
                <Link href="/sales/deliveries/new" className="btn btn-primary">
                    + Nuevo albarán
                </Link>
            </div>

            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Fecha entrega</th>
                            <th style={{ textAlign: 'right' }}>Líneas</th>
                            <th>Pedido venta</th>
                            <th>Registrado</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody>
                        {deliveries.length === 0 ? (
                            <tr><td colSpan={6} style={{ textAlign: 'center', color: 'var(--text-muted)', padding: 24 }}>Sin albaranes</td></tr>
                        ) : deliveries.map(d => (
                            <tr key={d.id}>
                                <td style={{ fontWeight: 700, fontFamily: 'monospace' }}>{d.number}</td>
                                <td>{new Date(d.deliveryDate).toLocaleDateString('es-ES')}</td>
                                <td style={{ textAlign: 'right' }}>{d.lineCount}</td>
                                <td>
                                    {d.salesOrderId ? (
                                        <Link href={`/sales/orders/${d.salesOrderId}`} style={{ fontSize: '12px', color: 'var(--brand-primary)' }}>
                                            Ver pedido
                                        </Link>
                                    ) : (
                                        <span style={{ color: 'var(--text-muted)' }}>—</span>
                                    )}
                                </td>
                                <td style={{ color: 'var(--text-muted)', fontSize: 12 }}>{new Date(d.createdAt).toLocaleDateString('es-ES')}</td>
                                <td>
                                    {d.salesOrderId && (
                                        <Link href={`/sales/orders/${d.salesOrderId}`} className="btn btn-secondary btn-sm">Detalle</Link>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </PageContainer>
    );
}
