"use client";
import React, { useState } from "react";
export default function LotsPage() {
  const [lots, setLots] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const fetchLots = async () => {
    setLoading(true);
    try {
      const res = await fetch(`/api/proxy/v1/inventory/lots`);
      const data = await res.json();
      setLots(data || []);
    } finally {
      setLoading(false);
    }
  };
  React.useEffect(() => { fetchLots(); }, []);
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Product Lots</h1>
      {loading && <p>Loading...</p>}
      <a href="/inventory/lots/new" className="px-4 py-2 bg-blue-600 text-white rounded">New Lot</a>
      <table className="w-full border mt-4">
        <thead>
          <tr className="bg-gray-200">
            <th className="border p-2">Lot Number</th>
            <th className="border p-2">Expiration</th>
            <th className="border p-2">Quantity</th>
            <th className="border p-2">Unit Cost</th>
          </tr>
        </thead>
        <tbody>
          {lots.map(l => (
            <tr key={l.id}>
              <td className="border p-2">{l.lotNumber}</td>
              <td className="border p-2">{l.expirationDate}</td>
              <td className="border p-2">{l.quantity}</td>
              <td className="border p-2">${l.unitCost}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
