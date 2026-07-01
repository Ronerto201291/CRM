"use client";
import React, { useState } from "react";
export default function CashFlowPage() {
  const [year, setYear] = useState(new Date().getFullYear());
  const [cashFlow, setCashFlow] = useState<any>(null);
  const generateCashFlow = async () => {
    const res = await fetch(`/api/v1/accounting/financial-statements/cash-flow`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(year)
    });
    const data = await res.json();
    setCashFlow(data);
  };
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Cash Flow Statement (EFE)</h1>
      <div className="mb-4">
        <label>Fiscal Year: </label>
        <input type="number" value={year} onChange={e => setYear(Number(e.target.value))} className="border p-2 w-24" />
        <button onClick={generateCashFlow} className="ml-2 px-4 py-2 bg-blue-600 text-white rounded">Generate</button>
      </div>
      {cashFlow && (
        <div className="border p-4">
          <div className="mb-2"><strong>Operating Activities:</strong> ${cashFlow.operatingActivitiesCash}</div>
          <div className="mb-2"><strong>Investing Activities:</strong> ${cashFlow.investingActivitiesCash}</div>
          <div className="mb-2"><strong>Financing Activities:</strong> ${cashFlow.financingActivitiesCash}</div>
          <div className="mb-2"><strong>Net Change in Cash:</strong> ${cashFlow.netChangeInCash}</div>
          <div className="font-bold"><strong>Ending Cash:</strong> ${cashFlow.endingCash}</div>
        </div>
      )}
    </div>
  );
}
