"use client";
import React, { useState } from "react";
export default function NewCustomerInvoicePage() {
  const [soId, setSoId] = useState("");
  const [number, setNumber] = useState("");
  const [invoiceDate, setInvoiceDate] = useState(new Date().toISOString().slice(0, 10));
  const [lines, setLines] = useState([{ salesOrderLineId: "", productId: "", billedQuantity: 0, unitPrice: 0 }]);
  const addLine = () => setLines([...lines, { salesOrderLineId: "", productId: "", billedQuantity: 0, unitPrice: 0 }]);
  const updateLine = (idx: number, key: string, value: any) => {
    const copy = [...lines];
    (copy[idx] as any)[key] = value;
    setLines(copy);
  };
  const submit = async () => {
    const body = { salesOrderId: soId, number, invoiceDate, lines };
    const res = await fetch('/api/v1/sales/invoices', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    if (!res.ok) {
      const err = await res.json();
      alert('Error: ' + (err.error || 'Unknown'));
      return;
    }
    alert('Created');
  };
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">New Customer Invoice</h1>
      <div><label>Sales Order Id</label><input value={soId} onChange={e => setSoId(e.target.value)} className="border p-2" /></div>
      <div><label>Number</label><input value={number} onChange={e => setNumber(e.target.value)} className="border p-2" /></div>
      <div><label>Invoice Date</label><input type="date" value={invoiceDate} onChange={e => setInvoiceDate(e.target.value)} className="border p-2" /></div>
      <div className="mt-4">
        <h2 className="font-semibold">Lines</h2>
        {lines.map((l, i) => (
          <div key={i} className="flex gap-2">
            <input placeholder="SO Line Id" value={l.salesOrderLineId} onChange={e => updateLine(i, 'salesOrderLineId', e.target.value)} className="border p-1" />
            <input placeholder="ProductId" value={l.productId} onChange={e => updateLine(i, 'productId', e.target.value)} className="border p-1" />
            <input type="number" value={l.billedQuantity} onChange={e => updateLine(i, 'billedQuantity', Number(e.target.value))} className="border p-1 w-20" />
            <input type="number" value={l.unitPrice} onChange={e => updateLine(i, 'unitPrice', Number(e.target.value))} className="border p-1 w-24" />
          </div>
        ))}
        <button onClick={addLine} className="mt-2 px-3 py-1 bg-blue-600 text-white rounded">Add line</button>
      </div>
      <button onClick={submit} className="mt-4 px-4 py-2 bg-green-600 text-white rounded">Create</button>
    </div>
  );
}
