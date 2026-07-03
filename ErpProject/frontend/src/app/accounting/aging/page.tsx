"use client";
import React, { useState } from "react";

interface AgingReport {
  type: string;
  totalAmount: number;
  current: number;
  days31To60: number;
  days61To90: number;
  days91Plus: number;
}

export default function AgingPage() {
  const [agingData, setAgingData] = useState<AgingReport | null>(null);
  const [dso, setDso] = useState<number | null>(null);
  const [dpo, setDpo] = useState<number | null>(null);
  const fetchAging = async (type: string) => {
    const res = await fetch(`/api/proxy/v1/accounting/aging/${type}`);
    const data = await res.json();
    setAgingData(data);
  };
  const fetchDSO = async () => {
    const res = await fetch(`/api/proxy/v1/accounting/aging/dso`);
    const data = await res.json();
    setDso(data.dso);
  };
  const fetchDPO = async () => {
    const res = await fetch(`/api/proxy/v1/accounting/aging/dpo`);
    const data = await res.json();
    setDpo(data.dpo);
  };
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Aging Analysis (Cobros/Pagos)</h1>
      <div className="mb-4">
        <button onClick={() => fetchAging("Receivables")} className="px-4 py-2 bg-blue-600 text-white rounded mr-2">Receivables Aging</button>
        <button onClick={() => fetchAging("Payables")} className="px-4 py-2 bg-blue-600 text-white rounded mr-2">Payables Aging</button>
        <button onClick={fetchDSO} className="px-4 py-2 bg-green-600 text-white rounded mr-2">Get DSO</button>
        <button onClick={fetchDPO} className="px-4 py-2 bg-green-600 text-white rounded">Get DPO</button>
      </div>
      {agingData && (
        <div className="border p-4">
          <div className="mb-2"><strong>Type:</strong> {agingData.type}</div>
          <div className="mb-2"><strong>Total Amount:</strong> ${agingData.totalAmount}</div>
          <div className="mb-2"><strong>Current (0-30 days):</strong> ${agingData.current}</div>
          <div className="mb-2"><strong>31-60 days:</strong> ${agingData.days31To60}</div>
          <div className="mb-2"><strong>61-90 days:</strong> ${agingData.days61To90}</div>
          <div className="mb-2"><strong>91+ days:</strong> ${agingData.days91Plus}</div>
        </div>
      )}
      {dso !== null && <div className="mt-4 p-4 border bg-blue-50"><strong>DSO (Days Sales Outstanding):</strong> {dso.toFixed(2)} days</div>}
      {dpo !== null && <div className="mt-4 p-4 border bg-green-50"><strong>DPO (Days Payable Outstanding):</strong> {dpo.toFixed(2)} days</div>}
    </div>
  );
}
