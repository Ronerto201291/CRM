"use client";

import React, { useState, useCallback, useEffect } from "react";
import PageContainer from "@/components/PageContainer";

interface SalesOrder {
    id: string;
    number: string;
    customerName: string;
}

interface DeliveryLine {
    salesOrderLineId?: string;
    productId?: string;
    description: string;
    shippedQuantity: number;
}

const emptyLine = (): DeliveryLine => ({
    description: '', shippedQuantity: 0,
});

export default function NewDeliveryNotePage() {
    const [orders, setOrders] = useState<SalesOrder[]>([]);
    const [selectedOrder, setSelectedOrder] = useState<SalesOrder | null>(null);
    const [form, setForm] = useState({
        salesOrderId: '',
        number: '',
        deliveryDate: new Date().toISOString().slice(0, 10),
        lines: [emptyLine()] as DeliveryLine[],
    });
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        fetch('/api/proxy/v1/sales/orders')
            .then(r => r.ok ? r.json() : [])
            .then(data => setOrders(Array.isArray(data) ? data : (data.items ?? [])))
            .catch(() => {});
    }, []);

    const selectOrder = (orderId: string) => {
        const order = orders.find(o => o.id === orderId);
        setSelectedOrder(order || null);
        setForm(f => ({ ...f, salesOrderId: orderId }));
    };

    const updateLine = (i: number, key: keyof DeliveryLine, val: any) => {
        const lines = [...form.lines];
        (lines[i] as any)[key] = val;
        setForm({ ...form, lines });
    };
    const addLine = () => setForm({ ...form, lines: [...form.lines, emptyLine()] });
    const removeLine = (i: number) => setForm({ ...form, lines: form.lines.filter((_, idx) => idx !== i) });

    const submit = async () => {
        if (!form.number) { alert('Introduce el número de albarán'); return; }
        setSaving(true);
        try {
            const res = await fetch('/api/proxy/v1/sales/deliveries', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(form),
            });
            if (res.ok) {
                alert('Albarán creado correctamente');
                window.location.href = '/sales/deliveries';
            } else {
                const e = await res.json();
                alert(e.error || e.message || 'Error al crear el albarán');
            }
        } finally {
            setSaving(false);
        }
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Nuevo Albarán de Entrega</h1>
                    <p className="page-subtitle">Registrar entrega de mercancía</p>
                </div>
                <a href="/sales/deliveries" className="btn btn-secondary">← Volver</a>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                <div className="form-group">
                    <label className="erp-label">PEDIDO DE VENTA</label>
                    <select className="erp-input" value={form.salesOrderId}
                        onChange={e => selectOrder(e.target.value)}>
                        <option value="">Seleccionar pedido (opcional)...</option>
                        {orders.map(o => <option key={o.id} value={o.id}>{o.number} — {o.customerName}</option>)}
                    </select>
                </div>
                <div className="form-group">
                    <label className="erp-label">NÚMERO DE ALBARÁN *</label>
                    <input className="erp-input" value={form.number}
                        onChange={e => setForm({ ...form, number: e.target.value })}
                        placeholder="ALB-2026-0001" />
                </div>
                <div className="form-group">
                    <label className="erp-label">FECHA DE ENTREGA</label>
                    <input type="date" className="erp-input" value={form.deliveryDate}
                        onChange={e => setForm({ ...form, deliveryDate: e.target.value })} />
                </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                    <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Líneas de Entrega</label>
                    <button className="btn btn-secondary btn-sm" onClick={addLine}>+ Añadir línea</button>
                </div>
                <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                        <thead style={{ background: 'var(--surface-2)' }}>
                            <tr>
                                <th style={{ padding: '8px 12px', textAlign: 'left', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)' }}>DESCRIPCIÓN</th>
                                <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '120px' }}>CANT. ENVIADA</th>
                                <th style={{ width: '30px', borderBottom: '1px solid var(--border)' }}></th>
                            </tr>
                        </thead>
                        <tbody>
                            {form.lines.map((line, i) => (
                                <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                                    <td style={{ padding: '6px 12px' }}>
                                        <input className="erp-input" style={{ fontSize: '12px', padding: '5px 8px' }}
                                            placeholder="Descripción del producto"
                                            value={line.description}
                                            onChange={e => updateLine(i, 'description', e.target.value)} />
                                    </td>
                                    <td style={{ padding: '6px 8px' }}>
                                        <input type="number" className="erp-input" style={{ fontSize: '12px', padding: '5px 8px', textAlign: 'right' }}
                                            value={line.shippedQuantity} min="0.01" step="0.01"
                                            onChange={e => updateLine(i, 'shippedQuantity', +e.target.value)} />
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

            <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                <a href="/sales/deliveries" className="btn btn-secondary">Cancelar</a>
                <button className="btn btn-primary" onClick={submit} disabled={saving}>
                    {saving ? 'Creando...' : '✓ Crear Albarán'}
                </button>
            </div>
        </PageContainer>
    );
}