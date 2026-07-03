"use client";
import React, { useState } from "react";
import Link from "next/link";
import PageListLayout from "@/components/PageListLayout";
import FormErrorBanner from "@/components/FormErrorBanner";
import FormLabel from "@/components/FormLabel";
import { updateLineAt } from "@/lib/lineForm";
import { customerSalesInvoiceCreateSchema } from "@/lib/schemas/purchasingSalesCreateSchemas";

interface CreateResult {
  id: string;
  billingInvoiceId: string;
  billingInvoiceNumber: string;
}

type InvoiceLine = { salesOrderLineId: string; productId: string; billedQuantity: number; unitPrice: number };

export default function NewCustomerInvoicePage() {
  const [soId, setSoId] = useState("");
  const [number, setNumber] = useState("");
  const [invoiceDate, setInvoiceDate] = useState(new Date().toISOString().slice(0, 10));
  const [lines, setLines] = useState<InvoiceLine[]>([
    { salesOrderLineId: "", productId: "", billedQuantity: 0, unitPrice: 0 },
  ]);
  const [result, setResult] = useState<CreateResult | null>(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const addLine = () => setLines([...lines, { salesOrderLineId: "", productId: "", billedQuantity: 0, unitPrice: 0 }]);
  const updateLine = <K extends keyof InvoiceLine>(idx: number, key: K, value: InvoiceLine[K]) => {
    setLines(updateLineAt(lines, idx, key, value));
  };

  const submit = async () => {
    setSaving(true);
    setResult(null);
    setFormError(null);

    const parsed = customerSalesInvoiceCreateSchema.safeParse({
      salesOrderId: soId,
      number,
      invoiceDate,
      lines,
    });
    if (!parsed.success) {
      setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
      setSaving(false);
      return;
    }

    const res = await fetch('/api/proxy/v1/sales/invoices', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        salesOrderId: soId,
        number,
        invoiceDate,
        lines,
      }),
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
    <PageListLayout
      title="Nueva factura de cliente"
      subtitle="Crea la factura comercial y enlaza la factura fiscal en Billing"
      actions={<Link href="/sales/invoices" className="btn btn-secondary">← Volver</Link>}
    >
      <FormErrorBanner message={formError} />

      <div className="erp-card" style={{ padding: '20px', maxWidth: '640px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
        <div>
          <FormLabel htmlFor="si-sales-order" required>Pedido de venta (ID)</FormLabel>
          <input id="si-sales-order" className="erp-input" value={soId} onChange={e => setSoId(e.target.value)} />
        </div>
        <div>
          <FormLabel htmlFor="si-number" required>Número interno</FormLabel>
          <input id="si-number" className="erp-input" value={number} onChange={e => setNumber(e.target.value)} />
        </div>
        <div>
          <FormLabel htmlFor="si-date" required>Fecha factura</FormLabel>
          <input id="si-date" type="date" className="erp-input" value={invoiceDate} onChange={e => setInvoiceDate(e.target.value)} />
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
          <button type="button" className="btn btn-secondary btn-sm" onClick={addLine}>Añadir línea</button>
        </div>

        <button type="button" className="btn btn-primary" onClick={submit} disabled={saving}>
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
          <Link href={`/billing/invoices/${result.billingInvoiceId}`} className="btn btn-secondary btn-sm" style={{ marginTop: '10px', display: 'inline-block' }}>
            Ver en Billing →
          </Link>
        </div>
      )}
    </PageListLayout>
  );
}
