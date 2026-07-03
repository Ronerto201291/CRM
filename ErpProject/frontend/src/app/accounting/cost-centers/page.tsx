"use client";
import React, { useEffect, useState } from "react";
export default function CostCentersPage() {
  const [centers, setCenters] = useState<any[]>([]);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [type, setType] = useState("Department");
  const fetchCenters = async () => {
    const res = await fetch(`/api/proxy/v1/accounting/cost-centers`);
    const data = await res.json();
    setCenters(data || []);
  };
  const create = async () => {
    await fetch(`/api/proxy/v1/accounting/cost-centers`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code, name, type })
    });
    setName("");
    setCode("");
    await fetchCenters();
  };
  useEffect(() => { fetchCenters(); }, []);
  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Cost Centers (Contabilidad Analítica)</h1>
      <div className="mb-4 border p-3">
        <input placeholder="Code" value={code} onChange={e => setCode(e.target.value)} className="border p-2 mr-2" />
        <input placeholder="Name" value={name} onChange={e => setName(e.target.value)} className="border p-2 mr-2" />
        <select value={type} onChange={e => setType(e.target.value)} className="border p-2 mr-2">
          <option>Department</option>
          <option>Product</option>
          <option>Project</option>
        </select>
        <button onClick={create} className="px-4 py-2 bg-green-600 text-white rounded">Add</button>
      </div>
      <table className="w-full border">
        <thead>
          <tr className="bg-gray-200">
            <th className="border p-2">Code</th>
            <th className="border p-2">Name</th>
            <th className="border p-2">Type</th>
            <th className="border p-2">Total Costs</th>
          </tr>
        </thead>
        <tbody>
          {centers.map(c => (
            <tr key={c.id}>
              <td className="border p-2">{c.code}</td>
              <td className="border p-2">{c.name}</td>
              <td className="border p-2">{c.type}</td>
              <td className="border p-2">${c.totalCosts}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
