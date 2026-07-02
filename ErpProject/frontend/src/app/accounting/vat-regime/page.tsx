"use client";
import React, { useState } from "react";
export default function VatRegimePage() {
  const [regime, setRegime] = useState("Standard");
  const [regimes, setRegimes] = useState<any[]>([]);
  const [effectiveDate, setEffectiveDate] = useState(new Date().toISOString().slice(0, 10));
  const setRegimeClick = async () => {
    await fetch(`/api/v1/accounting/vat/regime`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ type: regime, effectiveDate })
    });
    alert('Regime set');
  };
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">3.1 IVA - Régimen (Devengo/Caja/Prorrata)</h1>
      <div className="mb-4 border p-3">
        <label>Select VAT Regime:</label>
        <select value={regime} onChange={e => setRegime(e.target.value)} className="border p-2 mr-2">
          <option>Standard</option>
          <option>CashBasis</option>
          <option>Prorrata</option>
          <option>EquivalenceSurcharge</option>
          <option>InversionSubject</option>
        </select>
        <input type="date" value={effectiveDate} onChange={e => setEffectiveDate(e.target.value)} className="border p-2 mr-2" />
        <button onClick={setRegimeClick} className="px-4 py-2 bg-blue-600 text-white rounded">Set Regime</button>
      </div>
      <div className="bg-yellow-50 p-3 border">
        <strong>Current Regime:</strong> {regime}
        <p className="text-sm text-gray-600 mt-1">
          {regime === "Standard" && "Devengo: Recognition on invoice date"}
          {regime === "CashBasis" && "Caja: Recognition on payment date (Art. 163 undecies LIVA)"}
          {regime === "Prorrata" && "Prorrata: Deductible VAT based on revenue proportion"}
          {regime === "EquivalenceSurcharge" && "Recargo: Applied to RE suppliers"}
          {regime === "InversionSubject" && "ISP: Reverse charge for intra-community"}
        </p>
      </div>
    </div>
  );
}
