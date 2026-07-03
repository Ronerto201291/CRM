"use client";
import React, { useState } from "react";
export default function ISPPage() {
  const [countryCode, setCountryCode] = useState("DE");
  const [base, setBase] = useState(1000);
  const [vatRate, setVatRate] = useState(21);
  const [ispList, setIspList] = useState<any[]>([]);
  const create = async () => {
    const res = await fetch(`/api/proxy/v1/accounting/isp`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ supplierCountryCode: countryCode, vatableBase: base, vatRate })
    });
    const data = await res.json();
    alert('ISP created: ' + data.id);
    fetchList();
  };
  const fetchList = async () => {
    const res = await fetch(`/api/proxy/v1/accounting/isp`);
    const data = await res.json();
    setIspList(data);
  };
  React.useEffect(() => { fetchList(); }, []);
  const vatAmount = base * (vatRate / 100);
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">3.3 ISP - Inversión del Sujeto Pasivo (Reverse Charge)</h1>
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
          {ispList.map((i: any) => (
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
