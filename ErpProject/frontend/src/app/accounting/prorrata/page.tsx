"use client";
import React, { useState } from "react";
export default function ProrrataPage() {
  const [year, setYear] = useState(new Date().getFullYear());
  const [inlandRevenue, setInlandRevenue] = useState(100000);
  const [exemptRevenue, setExemptRevenue] = useState(20000);
  const [prorrata, setProrrata] = useState<any>(null);
  const calculate = async () => {
    const res = await fetch(`/api/v1/accounting/prorrata/calculate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        fiscalYear: year,
        inlandRevenue,
        exemptRevenue,
        type: 'General'
      })
    });
    const data = await res.json();
    setProrrata(data);
  };
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">3.2 Prorrata - Deductible VAT Calculation</h1>
      <div className="mb-4 border p-3">
        <div><label>Fiscal Year:</label> <input type="number" value={year} onChange={e => setYear(Number(e.target.value))} className="border p-2 w-24" /></div>
        <div className="mt-2"><label>Inland Revenue (Subject to VAT):</label> <input type="number" value={inlandRevenue} onChange={e => setInlandRevenue(Number(e.target.value))} className="border p-2" /></div>
        <div className="mt-2"><label>Exempt Revenue:</label> <input type="number" value={exemptRevenue} onChange={e => setExemptRevenue(Number(e.target.value))} className="border p-2" /></div>
        <button onClick={calculate} className="mt-3 px-4 py-2 bg-blue-600 text-white rounded">Calculate Prorrata</button>
      </div>
      {prorrata && (
        <div className="border p-4 bg-green-50">
          <div className="mb-2"><strong>Inland Revenue:</strong> €{prorrata.inlandRevenue}</div>
          <div className="mb-2"><strong>Exempt Revenue:</strong> €{prorrata.exemptRevenue}</div>
          <div className="mb-2 text-lg font-bold text-blue-600"><strong>Prorrata Percentage:</strong> {prorrata.prorrataPercentage.toFixed(2)}%</div>
          <p className="text-sm text-gray-600 mt-2">This percentage determines what portion of VAT is deductible on common expenses.</p>
        </div>
      )}
    </div>
  );
}
