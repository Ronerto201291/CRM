"use client";
import React, { useState } from "react";

interface SerialNumber {
  id: string;
  serial: string;
  productId: string;
  status: string;
  soldDate?: string;
}

export default function SerialsClient({ initialSerials }: { initialSerials: SerialNumber[] }) {
  const [serials, setSerials] = useState<SerialNumber[]>(initialSerials);
  const [loading, setLoading] = useState(false);
  const fetchSerials = async () => {
    setLoading(true);
    try {
      const res = await fetch(`/api/proxy/v1/inventory/serials`);
      const data = await res.json();
      setSerials(Array.isArray(data) ? data : (data.items ?? []));
    } finally {
      setLoading(false);
    }
  };
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Serial Numbers</h1>
      {loading && <p>Loading...</p>}
      <a href="/inventory/serials/new" className="px-4 py-2 bg-blue-600 text-white rounded">New Serial</a>
      <table className="w-full border mt-4">
        <thead>
          <tr className="bg-gray-200">
            <th className="border p-2">Serial</th>
            <th className="border p-2">Product</th>
            <th className="border p-2">Status</th>
            <th className="border p-2">Sold Date</th>
          </tr>
        </thead>
        <tbody>
          {serials.map(s => (
            <tr key={s.id}>
              <td className="border p-2">{s.serial}</td>
              <td className="border p-2">{s.productId}</td>
              <td className="border p-2">{s.status}</td>
              <td className="border p-2">{s.soldDate || '-'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
