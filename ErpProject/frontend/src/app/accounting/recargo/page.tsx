"use client";
import React, { useState, useEffect } from "react";

interface RecargoItem {
  supplierVatNumber: string;
  supplierIsRE: boolean;
  base: number;
  rechargeRate: number;
}

export default function RecargoPage() {
  const [supplierVatNumber, setSupplierVatNumber] = useState("");
  const [supplierIsRE, setSupplierIsRE] = useState(false);
  const [base, setBase] = useState(1000);
  const [rechargeRate, setRechargeRate] = useState(5.2);
  const [recargoList, setRecargoList] = useState<RecargoItem[]>([]);

  const fetchList = async () => {
    const res = await fetch(`/api/v1/accounting/recargo`);
    const data = await res.json();
    setRecargoList(Array.isArray(data) ? data : []);
  };

  useEffect(() => {
    fetchList();
  }, []);

  const create = async () => {
    await fetch(`/api/v1/accounting/recargo`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ supplierVatNumber, supplierIsRE, base, rechargeRate })
    });
    alert('Recargo created');
    fetchList();
  };

  const rechargeAmount = base * (rechargeRate / 100);

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">3.4 Recargo de Equivalencia (Extremo a Extremo)</h1>
      <div className="mb-4 border p-3">
        <div><label>Supplier VAT Number:</label> <input value={supplierVatNumber} onChange={(e) => setSupplierVatNumber(e.target.value)} className="border p-2 ml-2" /></div>
        <div className="mt-2"><label>Supplier is RE:</label> <input type="checkbox" checked={supplierIsRE} onChange={(e) => setSupplierIsRE(e.target.checked)} className="ml-2" /></div>
        <div className="mt-2"><label>Base:</label> <input type="number" value={base} onChange={(e) => setBase(Number(e.target.value))} className="border p-2 ml-2 w-24" /></div>
        <div className="mt-2"><label>Recharge Rate (%):</label> <input type="number" step="0.1" value={rechargeRate} onChange={(e) => setRechargeRate(Number(e.target.value))} className="border p-2 ml-2 w-24" /></div>
        <div className="mt-2 p-2 bg-yellow-50"><strong>Recharge Amount:</strong> €{rechargeAmount.toFixed(2)}</div>
        <button onClick={create} className="mt-3 px-4 py-2 bg-blue-600 text-white rounded">Create Recargo</button>
      </div>
      <table className="w-full border">
        <thead>
          <tr className="bg-gray-200">
            <th className="border p-2">Supplier VAT</th>
            <th className="border p-2">Base</th>
            <th className="border p-2">Recharge Rate</th>
            <th className="border p-2">Recharge Amount</th>
            <th className="border p-2">Modelo 303 Status</th>
          </tr>
        </thead>
        <tbody>
          {recargoList.map((r) => (
            <tr key={r.supplierVatNumber}>
              <td className="border p-2">{r.supplierVatNumber}</td>
              <td className="border p-2">€{r.base}</td>
              <td className="border p-2">{r.rechargeRate}%</td>
              <td className="border p-2">€{(r.base * (r.rechargeRate / 100)).toFixed(2)}</td>
              <td className="border p-2">Pending</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
