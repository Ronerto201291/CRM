"use client";
import React from "react";

interface SerialNumber {
  id: string;
  serial: string;
  productId: string;
  status: string;
  soldDate?: string;
}

export default function SerialsClient({ initialSerials }: { initialSerials: SerialNumber[] }) {
  const serials = initialSerials;
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Serial Numbers</h1>
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
