"use client";
import React, { useState } from "react";
import { ispCreateSchema } from '@/lib/schemas/accountingLegacyFormSchemas';

interface IspRecord {
  id: string;
  supplierCountryCode: string;
  vatableBase: number;
  vatRate: number;
  vatAmount: number;
  isReverseCharge: boolean;
}

export default function IspClient({ initialIspList }: { initialIspList: IspRecord[] }) {
  const [countryCode, setCountryCode] = useState("DE");
  const [base, setBase] = useState(1000);
  const [vatRate, setVatRate] = useState(21);
  const [ispList, setIspList] = useState<IspRecord[]>(initialIspList);
  const [formError, setFormError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const fetchList = async () => {
    const res = await fetch(`/api/proxy/v1/accounting/isp`);
    const data = await res.json();
    setIspList(Array.isArray(data) ? data : []);
  };

  const create = async () => {
    setFormError(null);
    setSuccessMsg(null);
    const parsed = ispCreateSchema.safeParse({ supplierCountryCode: countryCode, vatableBase: base, vatRate });
    if (!parsed.success) {
      setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
      return;
    }
    const res = await fetch(`/api/proxy/v1/accounting/isp`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ supplierCountryCode: countryCode, vatableBase: base, vatRate })
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      setFormError(err.error || 'Error al crear ISP');
      return;
    }
    const data = await res.json();
    setSuccessMsg(`ISP creado: ${data.id}`);
    fetchList();
  };

  const vatAmount = base * (vatRate / 100);

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">3.3 ISP - Inversión del Sujeto Pasivo (Reverse Charge)</h1>

      {(formError || successMsg) && (
        <div style={{
          padding: '12px 16px', marginBottom: 16, borderRadius: 8,
          color: formError ? 'var(--danger)' : 'var(--success)',
          background: formError ? 'var(--danger-bg)' : 'var(--success-bg)',
        }}>
          {formError || successMsg}
        </div>
      )}

      <div className="mb-4 border p-3">
        <div><label>Supplier Country:</label>
          <select value={countryCode} onChange={e => setCountryCode(e.target.value)} className="border p-2 ml-2">
            <option>DE</option><option>FR</option><option>IT</option><option>BE</option><option>NL</option>
          </select>
        </div>
        <div className="mt-2"><label>Vatable Base:</label> <input type="number" value={base} onChange={e => setBase(Number(e.target.value))} className="border p-2 ml-2 w-32" /></div>
        <div className="mt-2"><label>VAT Rate (%):</label> <input type="number" value={vatRate} onChange={e => setVatRate(Number(e.target.value))} className="border p-2 ml-2 w-24" /></div>
        <div className="mt-2 p-2 bg-blue-50"><strong>VAT Amount (Reverse Charged):</strong> €{vatAmount.toFixed(2)}</div>
        <button onClick={create} className="mt-3 px-4 py-2 bg-blue-600 text-white rounded">Create ISP</button>
      </div>
      <table className="w-full border">
        <thead>
          <tr className="bg-gray-200">
            <th className="border p-2">Country</th>
            <th className="border p-2">Base</th>
            <th className="border p-2">VAT Rate</th>
            <th className="border p-2">VAT Amount</th>
            <th className="border p-2">Reverse Charge</th>
          </tr>
        </thead>
        <tbody>
          {ispList.map((i) => (
            <tr key={i.id}>
              <td className="border p-2">{i.supplierCountryCode}</td>
              <td className="border p-2">€{i.vatableBase}</td>
              <td className="border p-2">{i.vatRate}%</td>
              <td className="border p-2">€{i.vatAmount.toFixed(2)}</td>
              <td className="border p-2">{i.isReverseCharge ? 'Yes' : 'No'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
