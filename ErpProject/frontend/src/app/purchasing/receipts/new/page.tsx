"use client";

import React, { useState } from "react";
import PageContainer from "@/components/PageContainer";
import { updateLineAt } from "@/lib/lineForm";
import { PurchaseOrder } from "@/types/api";

interface ReceiptLine {
    purchaseOrderLineId?: string;
    productId?: string;
    description: string;
    quantityReceived: number;
}

const emptyLine = (): ReceiptLine => ({
    description: '', quantityReceived: 0,
});

export default function NewReceiptPage() {
    const [orders, setOrders] = useState<PurchaseOrder[]>([]);
    const [selectedOrder, setSelectedOrder] = useState<PurchaseOrder | null>(null);
    const [form, setForm] = useState({
        purchaseOrderId: '',
        number: '',
        receiptDate: new Date().toISOString().slice(0, 10),
        lines: [emptyLine()] as ReceiptLine[],
    });
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);

    React.useEffect(() => {
        fetch('/api/proxy/v1/purchasing/orders')
            .then(r => r.ok ? r.json() : [])
            .then(data => setOrders(Array.isArray(data) ? data : (data.items ?? [])))
            .catch(() => {});
    }, []);

    const selectOrder = (orderId: string) => {
        const order = orders.find(o => o.id === orderId);
        setSelectedOrder(order || null);
        setForm(f => ({ ...f, purchaseOrderId: orderId }));
    };

    const updateLine = <K extends keyof ReceiptLine>(i: number, key: K, val: ReceiptLine[K]) =>
        setForm({ ...form, lines: updateLineAt(form.lines, i, key, val) });
    const addLine = () => setForm({ ...form, lines: [...form.lines, emptyLine()] });
    const removeLine = (i: number) => setForm({ ...form, lines: form.lines.filter((_, idx) => idx !== i) });

    const submit = async () => {
        setFormError(null);
        if (!form.number) { setFormError('Introduce el número de recepción'); return; }
        setSaving(true);
        try {
            const res = await fetch('/api/proxy/v1/purchasing/receipts', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(form),
            });
            if (res.ok) {
                window.location.href = '/purchasing/receipts';
            } else {
                const e = await res.json();
                setFormError(e.error || e.message || 'Error al crear la recepción');
            }
        } finally {
            setSaving(false);
        }
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Nueva Recepción de Compra</h1>
                    <p className="page-subtitle">Registrar recepción de mercancía</p>
                </div>
                <a href="/purchasing/receipts" className="btn btn-secondary">← Volver</a>
            </div>

            {formError && (
                <div className="erp-card" style={{ padding: '12px 16px', marginBottom: 16, color: 'var(--danger)', background: 'var(--danger-bg)' }}>
                    {formError}
                </div>
            )}

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                <div className="form-group">
                    <label className="erp-label">PEDido de COMPRA</label>
                    <select className="erp-input" value={form.purchaseOrderId}
                        onChange={e => selectOrder(e.target.value)}>
                        <option value="">Seleccionar pedido...</option>
                        {orders.map(o => <option key={o.id} value={o.id}>{o.number} — {o.supplierName}</option>)}
                    </select>
                </div>
                <div className="form-group">
                    <label className="erp-label">NÚMERO DE RECEPCIÓN *</label>
                    <input className="erp-input" value={form.number}
                        onChange={e => setForm({ ...form, number: e.target.value })}
                        placeholder="RC-2026-0001" />
                </div>
                <div className="form-group">
                    <label className="erp-label">FECHA DE RECEPCIÓN</label>
                    <input type="date" className="erp-input" value={form.receiptDate}
                        onChange={e => setForm({ ...form, receiptDate: e.target.value })} />
                </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                    <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Líneas de Recepción</label>
                    <button className="btn btn-secondary btn-sm" onClick={addLine}>+ Añadir línea</button>
                </div>
                <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                        <thead style={{ background: 'var(--surface-2)' }}>
                            <tr>
                                <th style={{ padding: '8px 12px', textAlign: 'left', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)' }}>DESCRIPCIÓN</th>
                                <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '120px' }}>CANT. RECIBIDA</th>
                                <th style={{ width: '30px', borderBottom: '1px solid var(--border)' }}></th>
                            </tr>
                        </thead>
                        <tbody>
                            {form.lines.map((line, i) => (
                                <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                                    <td style={{ padding: '6px 12px' }}>
                                        <input className="erp-input" style={{ fontSize: '12px', padding: '5px 8px' }}
                                            placeholder="Descripción del producto recibido"
                                            value={line.description}
                                            onChange={e => updateLine(i, 'description', e.target.value)} />
                                    </td>
                                    <td style={{ padding: '6px 8px' }}>
                                        <input type="number" className="erp-input" style={{ fontSize: '12px', padding: '5px 8px', textAlign: 'right' }}
                                            value={line.quantityReceived} min="0.01" step="0.01"
                                            onChange={e => updateLine(i, 'quantityReceived', +e.target.value)} />
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
                <a href="/purchasing/receipts" className="btn btn-secondary">Cancelar</a>
                <button className="btn btn-primary" onClick={submit} disabled={saving}>
                    {saving ? 'Creando...' : '✓ Crear Recepción'}
                </button>
            </div>
        </PageContainer>
    );
}
