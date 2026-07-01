"use client";
import React, { useState } from "react";
export default function InventoryValuationPage() {
  const [method, setMethod] = useState("PMP");
  const [products, setProducts] = useState<any[]>([]);
  const fetchValuation = async () => {
    const res = await fetch(`/api/v1/inventory/valuation?method=${method}`);
    const data = await res.json();
    setProducts(data || []);
  };
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Inventory Valuation</h1>
      <div className="mb-4">
        <label>Method: </label>
        <select value={method} onChange={e => setMethod(e.target.value)} className="border p-2">
          <option>PMP</option>
          <option>FIFO</option>
        </select>
        <button onClick={fetchValuation} className="ml-2 px-4 py-2 bg-blue-600 text-white rounded">Calculate</button>
      </div>
      <table className="w-full border">
        <thead>
          <tr className="bg-gray-200">
            <th className="border p-2">Product</th>
            <th className="border p-2">Quantity</th>
            <th className="border p-2">Unit Cost</th>
            <th className="border p-2">Total Value</th>
          </tr>
        </thead>
        <tbody>
          {products.map(p => (
            <tr key={p.id}>
              <td className="border p-2">{p.name}</td>
              <td className="border p-2">{p.quantity}</td>
              <td className="border p-2">${p.unitCost}</td>
              <td className="border p-2">${p.totalValue}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
