"use client";
import React, { useState } from "react";

interface ViesValidation {
  countryCode: string;
  vatNumber: string;
  isValid: boolean;
  validationStatus: string;
}

export default function ViesPage() {
  const [countryCode, setCountryCode] = useState("DE");
  const [vatNumber, setVatNumber] = useState("");
  const [validation, setValidation] = useState<ViesValidation | null>(null);
    const validate = async () => {
    const fullVat = `${countryCode}${vatNumber}`.toUpperCase();
    const res = await fetch(`/api/proxy/v1/accounting/vies/validate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ countryCode, vatNumberOnly: vatNumber, vatNumber: fullVat }),
    });
    const data = await res.json();
    setValidation(data);
  };
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">3.5 VIES - Intra-Community VAT Validation</h1>
      <div className="mb-4 border p-3">
        <div><label>Country Code:</label>
          <select value={countryCode} onChange={e => setCountryCode(e.target.value)} className="border p-2 ml-2">
            <option>DE</option><option>FR</option><option>IT</option><option>BE</option><option>NL</option><option>AT</option>
          </select>
        </div>
        <div className="mt-2"><label>VAT Number:</label> <input value={vatNumber} onChange={e => setVatNumber(e.target.value)} className="border p-2 ml-2" placeholder="e.g., 123456789" /></div>
        <button onClick={validate} className="mt-3 px-4 py-2 bg-blue-600 text-white rounded">Validate VIES</button>
      </div>
      {validation && (
        <div className={`border p-4 ${validation.isValid ? 'bg-green-50' : 'bg-red-50'}`}>
          <div><strong>Country:</strong> {validation.countryCode}</div>
          <div><strong>VAT Number:</strong> {validation.vatNumber}</div>
          <div className={`font-bold ${validation.isValid ? 'text-green-600' : 'text-red-600'}`}>Status: {validation.validationStatus}</div>
          <p className="text-sm text-gray-600 mt-2">VIES validation ensures supplier VAT is valid for intra-community operations.</p>
        </div>
      )}
    </div>
  );
}
