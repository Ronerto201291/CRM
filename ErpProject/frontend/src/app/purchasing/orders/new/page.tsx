"use client";

import React, { useState, useEffect } from "react";
import PageContainer from "@/components/PageContainer";
import { Supplier } from "@/types/api";

interface OrderLine {
    productId?: string;
    description: string;
    quantity: number;
    unitPrice: number;
    taxRate: number;
}

const emptyLine = (): OrderLine => ({
    description: '', quantity: 1, unitPrice: 0, taxRate: 21,
});

export default function NewPurchaseOrderPage() {
    const [suppliers, setSuppliers] = useState<Supplier[]>([]);
    const [form, setForm] = useState({
        supplierId: '',
        number: '',
        orderDate: new Date().toISOString().slice(0, 10),
        notes: '',
        lines: [emptyLine()] as OrderLine[],
    });
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        fetch('/api/proxy/suppliers')
            .then(r => r.ok ? r.json() : [])
            .then(data => setSuppliers(Array.isArray(data) ? data : (data.items ?? [])))
            .catch(() => {});
    }, []);

    const updateLine = (i: number, key: keyof OrderLine, val: any) => {
        const lines = [...form.lines];
        (lines[i] as any)[key] = val;
        setForm({ ...form, lines });
    };
    const addLine = () => setForm({ ...form, lines: [...form.lines, emptyLine()] });
    const removeLine = (i: number) => setForm({ ...form, lines: form.lines.filter((_, idx) => idx !== i) });

    const subtotal = form.lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
    const taxAmount = form.lines.reduce((s, l) => s + l.quantity * l.unitPrice * (l.taxRate / 100), 0);
    const total = subtotal + taxAmount;
    const fmt = (n: number) => `€ ${n.toLocaleString('es-ES', { minimumFractionDigits: 2 })}`;

    const submit = async () => {
        if (!form.supplierId) { alert('Selecciona un proveedor'); return; }
        if (!form.number) { alert('Introduce el número de pedido'); return; }
        setSaving(true);
        try {
            const body = {
                supplierId: form.supplierId,
                number: form.number,
                orderDate: form.orderDate,
                notes: form.notes,
                lines: form.lines,
            };
            const res = await fetch('/api/proxy/v1/purchasing/orders', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body),
            });
            if (res.ok) {
                alert('Pedido creado correctamente');
                window.location.href = '/purchasing/orders';
            } else {
                const e = await res.json();
                alert(e.error || e.message || 'Error al crear el pedido');
            }
        } finally {
            setSaving(false);
        }
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Nuevo Pedido de Compra</h1>
                    <p className="page-subtitle">Registrar pedido a proveedor</p>
                </div>
                <a href="/purchasing/orders" className="btn btn-secondary">← Volver</a>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                <div className="form-group">
                    <label className="erp-label">PROVEEDOR *</label>
                    <select className="erp-input" value={form.supplierId}
                        onChange={e => setForm({ ...form, supplierId: e.target.value })}>
                        <option value="">Seleccionar proveedor...</option>
                        {suppliers.map(s => <option key={s.id} value={s.id}>{s.name} {s.taxId ? `(${s.taxId})` : ''}</option>)}
                    </select>
                </div>
                <div className="form-group">
                    <label className="erp-label">NÚMERO DE PEDIDO *</label>
                    <input className="erp-input" value={form.number}
                        onChange={e => setForm({ ...form, number: e.target.value })}
                        placeholder="PC-2026-0001" />
                </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                <div className="form-group">
                    <label className="erp-label">FECHA DE PEDIDO</label>
                    <input type="date" className="erp-input" value={form.orderDate}
                        onChange={e => setForm({ ...form, orderDate: e.target.value })}
                        style={{ maxWidth: '200px' }} />
                </div>
                <div className="form-group">
                    <label className="erp-label">NOTAS</label>
                    <input className="erp-input" value={form.notes}
                        onChange={e => setForm({ ...form, notes: e.target.value })}
                        placeholder="Notas internas opcionales" />
                </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                    <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase' }}>Líneas</label>
                    <button className="btn btn-secondary btn-sm" onClick={addLine}>+ Añadir línea</button>
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
                                            <button onClick={() => removeLine(i)} style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--danger)', fontSize: '16px' }}>✕</button>
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
                <a href="/purchasing/orders" className="btn btn-secondary">Cancelar</a>
                <button className="btn btn-primary" onClick={submit} disabled={saving}>
                    {saving ? 'Creando...' : '✓ Crear Pedido'}
                </button>
            </div>
        </PageContainer>
    );
}