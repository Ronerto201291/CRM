"use client";

import React, { useState } from "react";
import Link from "next/link";
import PageListLayout from "@/components/PageListLayout";
import FormErrorBanner from "@/components/FormErrorBanner";
import FormLabel from "@/components/FormLabel";
import { updateLineAt } from "@/lib/lineForm";
import { parseListResponse } from "@/lib/parseListResponse";
import { supplierInvoiceCreateSchema } from "@/lib/schemas/purchasingSalesCreateSchemas";
import { PurchaseOrder, Supplier } from "@/types/api";

interface PurchaseOrderLine {
    id: string;
    productId?: string;
    quantity: number;
    unitPrice: number;
}

interface PurchaseOrderDetail extends PurchaseOrder {
    lines: PurchaseOrderLine[];
}

interface InvoiceLine {
    purchaseOrderLineId: string;
    productId?: string;
    description: string;
    quantity: number;
    unitPrice: number;
    taxRate: number;
}

const emptyLine = (): InvoiceLine => ({
    purchaseOrderLineId: '',
    description: '',
    quantity: 1,
    unitPrice: 0,
    taxRate: 21,
});

export default function NewSupplierInvoicePage() {
    const [orders, setOrders] = useState<PurchaseOrder[]>([]);
    const [suppliers, setSuppliers] = useState<Supplier[]>([]);
    const [form, setForm] = useState({
        supplierId: '',
        purchaseOrderId: '',
        number: '',
        invoiceDate: new Date().toISOString().slice(0, 10),
        lines: [emptyLine()] as InvoiceLine[],
    });
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);

    React.useEffect(() => {
        fetch('/api/proxy/v1/purchasing/orders')
            .then(r => r.ok ? r.json() : [])
            .then(data => setOrders(Array.isArray(data) ? data : (data.items ?? [])))
            .catch(() => {});
        fetch('/api/proxy/suppliers?pageSize=500')
            .then(r => r.ok ? r.json() : [])
            .then(data => setSuppliers(parseListResponse<Supplier>(data)))
            .catch(() => {});
    }, []);

    const selectOrder = async (orderId: string) => {
        if (!orderId) {
            setForm(f => ({ ...f, purchaseOrderId: '', lines: [emptyLine()] }));
            return;
        }
        const res = await fetch(`/api/proxy/v1/purchasing/orders/${orderId}`);
        if (!res.ok) {
            setFormError('No se pudo cargar el pedido de compra');
            return;
        }
        const order = (await res.json()) as PurchaseOrderDetail;
        setForm(f => ({
            ...f,
            purchaseOrderId: orderId,
            supplierId: order.supplierId ?? f.supplierId,
            lines: order.lines.length > 0
                ? order.lines.map(l => ({
                    purchaseOrderLineId: l.id,
                    productId: l.productId,
                    description: l.productId ? `Producto ${l.productId}` : `Línea pedido ${l.id.slice(0, 8)}`,
                    quantity: l.quantity,
                    unitPrice: l.unitPrice,
                    taxRate: 21,
                }))
                : [emptyLine()],
        }));
    };

    const updateLine = <K extends keyof InvoiceLine>(i: number, key: K, val: InvoiceLine[K]) =>
        setForm({ ...form, lines: updateLineAt(form.lines, i, key, val) });
    const addLine = () => setForm({ ...form, lines: [...form.lines, emptyLine()] });
    const removeLine = (i: number) => setForm({ ...form, lines: form.lines.filter((_, idx) => idx !== i) });

    const subtotal = form.lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
    const taxAmount = form.lines.reduce((s, l) => s + l.quantity * l.unitPrice * (l.taxRate / 100), 0);
    const total = subtotal + taxAmount;
    const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    const submit = async () => {
        setFormError(null);
        const parsed = supplierInvoiceCreateSchema.safeParse(form);
        if (!parsed.success) {
            setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
            return;
        }
        setSaving(true);
        try {
            const body = {
                supplierId: form.supplierId,
                purchaseOrderId: form.purchaseOrderId,
                number: form.number,
                invoiceDate: form.invoiceDate,
                totalAmount: total,
                lines: form.lines.map(l => ({
                    purchaseOrderLineId: l.purchaseOrderLineId,
                    productId: l.productId || null,
                    quantity: l.quantity,
                    unitPrice: l.unitPrice,
                })),
            };
            const res = await fetch('/api/proxy/v1/purchasing/invoices', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body),
            });
            if (res.ok) {
                window.location.href = '/purchasing/invoices';
            } else {
                const e = await res.json();
                setFormError(e.error || e.message || 'Error al crear la factura');
            }
        } finally {
            setSaving(false);
        }
    };

    return (
        <PageListLayout
            title="Nueva Factura de Proveedor"
            subtitle="Registrar factura de compra contra un pedido aprobado"
            actions={<Link href="/purchasing/invoices" className="btn btn-secondary">← Volver</Link>}
        >
            <FormErrorBanner message={formError} />

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                <div className="form-group">
                    <FormLabel htmlFor="pi-supplier" required>Proveedor (CRM)</FormLabel>
                    <select id="pi-supplier" className="erp-input" value={form.supplierId}
                        onChange={e => setForm({ ...form, supplierId: e.target.value })}>
                        <option value="">Seleccionar proveedor...</option>
                        {suppliers.map(s => (
                            <option key={s.id} value={s.id}>{s.name} ({s.taxId})</option>
                        ))}
                    </select>
                </div>
                <div className="form-group">
                    <FormLabel htmlFor="pi-order" required>Pedido de compra</FormLabel>
                    <select id="pi-order" className="erp-input" value={form.purchaseOrderId}
                        onChange={e => selectOrder(e.target.value)}>
                        <option value="">Seleccionar pedido...</option>
                        {orders.filter(o => o.status === 'Approved').map(o => (
                            <option key={o.id} value={o.id}>{o.number}</option>
                        ))}
                    </select>
                </div>
                <div className="form-group">
                    <FormLabel htmlFor="pi-number" required>Número de factura</FormLabel>
                    <input id="pi-number" className="erp-input" value={form.number}
                        onChange={e => setForm({ ...form, number: e.target.value })}
                        placeholder="FV-2026-0001" />
                </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
                <FormLabel htmlFor="pi-date">Fecha</FormLabel>
                <input id="pi-date" type="date" className="erp-input" value={form.invoiceDate}
                    onChange={e => setForm({ ...form, invoiceDate: e.target.value })}
                    style={{ maxWidth: '200px' }} />
            </div>

            <div style={{ marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                    <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Líneas</label>
                    <button type="button" className="btn btn-secondary btn-sm" onClick={addLine}>+ Añadir línea</button>
                </div>
                <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                        <thead style={{ background: 'var(--surface-2)' }}>
                            <tr>
                                <th style={{ padding: '8px 12px', textAlign: 'left', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)' }}>DESCRIPCIÓN</th>
                                <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '70px' }}>CANT.</th>
                                <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '100px' }}>P. UNIT.</th>
                                <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '70px' }}>IVA</th>
                                <th style={{ padding: '8px 8px', textAlign: 'right', fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)', width: '90px' }}>TOTAL</th>
                                <th style={{ width: '30px', borderBottom: '1px solid var(--border)' }}></th>
                            </tr>
                        </thead>
                        <tbody>
                            {form.lines.map((line, i) => (
                                <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                                    <td style={{ padding: '6px 12px' }}>
                                        <input className="erp-input" style={{ fontSize: '12px', padding: '5px 8px' }}
                                            placeholder="Descripción"
                                            value={line.description}
                                            onChange={e => updateLine(i, 'description', e.target.value)} />
                                    </td>
                                    <td style={{ padding: '6px 8px' }}>
                                        <input type="number" className="erp-input" style={{ fontSize: '12px', padding: '5px 8px', textAlign: 'right' }}
                                            value={line.quantity} min="0.01" step="0.01"
                                            onChange={e => updateLine(i, 'quantity', +e.target.value)} />
                                    </td>
                                    <td style={{ padding: '6px 8px' }}>
                                        <input type="number" className="erp-input" style={{ fontSize: '12px', padding: '5px 8px', textAlign: 'right' }}
                                            value={line.unitPrice} min="0" step="0.01"
                                            onChange={e => updateLine(i, 'unitPrice', +e.target.value)} />
                                    </td>
                                    <td style={{ padding: '6px 8px' }}>
                                        <select className="erp-input" style={{ fontSize: '12px', padding: '5px 8px' }}
                                            value={line.taxRate}
                                            onChange={e => updateLine(i, 'taxRate', +e.target.value)}>
                                            <option value={21}>21%</option>
                                            <option value={10}>10%</option>
                                            <option value={4}>4%</option>
                                            <option value={0}>Exento</option>
                                        </select>
                                    </td>
                                    <td style={{ padding: '6px 8px', textAlign: 'right', fontSize: '13px', fontWeight: 600 }}>
                                        {fmt(line.quantity * line.unitPrice * (1 + line.taxRate / 100))}
                                    </td>
                                    <td style={{ padding: '6px 4px', textAlign: 'center' }}>
                                        {form.lines.length > 1 && (
                                            <button type="button" onClick={() => removeLine(i)} style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--danger)', fontSize: '16px' }}>✕</button>
                                        )}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>

            <div style={{ background: 'var(--surface-2)', borderRadius: '8px', padding: '14px 16px', marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '40px', fontSize: '13px' }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', minWidth: '200px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between' }}><span style={{ color: 'var(--text-secondary)' }}>Base imponible</span><span style={{ fontWeight: 600 }}>{fmt(subtotal)}</span></div>
                        <div style={{ display: 'flex', justifyContent: 'space-between' }}><span style={{ color: 'var(--text-secondary)' }}>IVA</span><span style={{ fontWeight: 600 }}>{fmt(taxAmount)}</span></div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid var(--border)', paddingTop: '6px', marginTop: '2px' }}>
                            <span style={{ fontWeight: 800, fontSize: '15px' }}>Total</span>
                            <span style={{ fontWeight: 800, fontSize: '15px', color: 'var(--brand-primary)' }}>{fmt(total)}</span>
                        </div>
                    </div>
                </div>
            </div>

            <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                <Link href="/purchasing/invoices" className="btn btn-secondary">Cancelar</Link>
                <button type="button" className="btn btn-primary" onClick={submit} disabled={saving}>
                    {saving ? 'Creando...' : '✓ Crear Factura'}
                </button>
            </div>
        </PageListLayout>
    );
}
