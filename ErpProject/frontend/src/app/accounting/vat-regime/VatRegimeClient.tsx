"use client";
import React, { useState } from "react";

export default function VatRegimeClient() {
  const [regime, setRegime] = useState("Standard");
  const [effectiveDate, setEffectiveDate] = useState(new Date().toISOString().slice(0, 10));
  const [formError, setFormError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const setRegimeClick = async () => {
    setFormError(null);
    setSuccessMsg(null);
    const res = await fetch(`/api/proxy/v1/accounting/vat/regime`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ type: regime, effectiveDate })
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      setFormError(err.error || 'Error al establecer régimen');
      return;
    }
    setSuccessMsg('Régimen de IVA actualizado');
  };

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">3.1 IVA - Régimen (Devengo/Caja/Prorrata)</h1>

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
