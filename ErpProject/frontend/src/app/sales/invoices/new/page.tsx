"use client";
import React, { useState } from "react";
import { updateLineAt } from "@/lib/lineForm";

interface CreateResult {
  id: string;
  billingInvoiceId: string;
  billingInvoiceNumber: string;
}

export default function NewCustomerInvoicePage() {
  const [soId, setSoId] = useState("");
  const [number, setNumber] = useState("");
  const [invoiceDate, setInvoiceDate] = useState(new Date().toISOString().slice(0, 10));
  const [lines, setLines] = useState([{ salesOrderLineId: "", productId: "", billedQuantity: 0, unitPrice: 0 }]);
  const [result, setResult] = useState<CreateResult | null>(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  type InvoiceLine = { salesOrderLineId: string; productId: string; billedQuantity: number; unitPrice: number };

  const addLine = () => setLines([...lines, { salesOrderLineId: "", productId: "", billedQuantity: 0, unitPrice: 0 }]);
  const updateLine = <K extends keyof InvoiceLine>(idx: number, key: K, value: InvoiceLine[K]) => {
    setLines(updateLineAt(lines, idx, key, value));
  };

  const submit = async () => {
    setSaving(true);
    setResult(null);
    setFormError(null);
    const body = { salesOrderId: soId, number, invoiceDate, lines };
    const res = await fetch('/api/proxy/v1/sales/invoices', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    setSaving(false);
    if (!res.ok) {
      const err = await res.json();
      setFormError(err.error || 'Error desconocido');
      return;
    }
    setResult(await res.json());
  };

  return (
    <div style={{ padding: '28px 32px', fontFamily: 'Inter, sans-serif' }}>
      <h1 className="page-title">Nueva factura de cliente</h1>
      <p className="page-subtitle">Crea la factura comercial y enlaza la factura fiscal en Billing.</p>

      {formError && (
        <div className="erp-card" style={{ padding: '12px 16px', marginBottom: 16, maxWidth: 640, color: 'var(--danger)', background: 'var(--danger-bg)' }}>
          {formError}
        </div>
      )}

      <div className="erp-card" style={{ padding: '20px', maxWidth: '640px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
        <div>
          <label className="erp-label">Pedido de venta (ID)</label>
          <input className="erp-input" value={soId} onChange={e => setSoId(e.target.value)} />
        </div>
        <div>
          <label className="erp-label">Número interno</label>
          <input className="erp-input" value={number} onChange={e => setNumber(e.target.value)} />
        </div>
        <div>
          <label className="erp-label">Fecha factura</label>
          <input type="date" className="erp-input" value={invoiceDate} onChange={e => setInvoiceDate(e.target.value)} />
        </div>

        <div>
          <h2 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '8px' }}>Líneas</h2>
          {lines.map((l, i) => (
            <div key={i} style={{ display: 'flex', gap: '8px', marginBottom: '8px', flexWrap: 'wrap' }}>
              <input className="erp-input" placeholder="Línea pedido ID" value={l.salesOrderLineId}
                onChange={e => updateLine(i, 'salesOrderLineId', e.target.value)} style={{ flex: 1, minWidth: '140px' }} />
              <input className="erp-input" placeholder="Producto ID" value={l.productId}
                onChange={e => updateLine(i, 'productId', e.target.value)} style={{ flex: 1, minWidth: '120px' }} />
              <input type="number" className="erp-input" placeholder="Cant." value={l.billedQuantity}
                onChange={e => updateLine(i, 'billedQuantity', Number(e.target.value))} style={{ width: '80px' }} />
              <input type="number" className="erp-input" placeholder="Precio" value={l.unitPrice}
                onChange={e => updateLine(i, 'unitPrice', Number(e.target.value))} style={{ width: '100px' }} />
            </div>
          ))}
          <button className="btn btn-secondary btn-sm" onClick={addLine}>Añadir línea</button>
        </div>

        <button className="btn btn-primary" onClick={submit} disabled={saving}>
          {saving ? 'Creando...' : 'Crear factura'}
        </button>
      </div>

      {result && (
        <div className="erp-card" style={{ padding: '16px', maxWidth: '640px', marginTop: '16px', background: 'var(--success-bg)' }}>
          <p style={{ fontWeight: 600, marginBottom: '8px' }}>Factura creada correctamente</p>
          <p style={{ fontSize: '13px', margin: '4px 0' }}>ID ventas: {result.id}</p>
          <p style={{ fontSize: '13px', margin: '4px 0' }}>
            Factura fiscal: <strong>{result.billingInvoiceNumber}</strong>
          </p>
          <a href={`/billing/invoices/${result.billingInvoiceId}`} className="btn btn-secondary btn-sm" style={{ marginTop: '10px', display: 'inline-block' }}>
            Ver en Billing →
          </a>
        </div>
      )}
    </div>
  );
}
