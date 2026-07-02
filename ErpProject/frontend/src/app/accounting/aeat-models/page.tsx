"use client";
import React, { useState } from "react";

export default function AeatPage() {
  const [models] = useState([
    { id: 1, type: "347", year: 2024, status: "Draft", totalRecords: 45, totalAmount: 500000 },
    { id: 2, type: "111", year: 2025, month: 1, status: "Generated", netVat: 35000 },
    { id: 3, type: "200", year: 2024, status: "Submitted", annualVat: 250000 },
  ]);

  return (
    <div className="p-6">
      <h1 className="text-3xl font-bold mb-6">0.5 Modelos AEAT</h1>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div className="border rounded-lg p-4 bg-blue-50">
          <p className="text-gray-600 text-sm">Modelo 347 (Anual)</p>
          <p className="text-sm">Operaciones 3.005€+</p>
        </div>
        <div className="border rounded-lg p-4 bg-green-50">
          <p className="text-gray-600 text-sm">Modelos 111/190 (Trimestral/Mensual)</p>
          <p className="text-sm">IVA Declaratorio</p>
        </div>
        <div className="border rounded-lg p-4 bg-yellow-50">
          <p className="text-gray-600 text-sm">Modelo 200 (Anual)</p>
          <p className="text-sm">Resumen anual IVA</p>
        </div>
        <div className="border rounded-lg p-4 bg-purple-50">
          <p className="text-gray-600 text-sm">Modelo 202 (Devolución)</p>
          <p className="text-sm">IVA a devolver</p>
        </div>
      </div>

      <div className="border rounded-lg p-4 mb-6">
        <h2 className="text-xl font-semibold mb-4">Modelos Generados</h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-200">
              <th className="border p-2 text-left">Modelo</th>
              <th className="border p-2 text-left">Período</th>
              <th className="border p-2 text-right">Cantidad</th>
              <th className="border p-2">Estado</th>
              <th className="border p-2">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {models.map((m) => (
              <tr key={m.id}>
                <td className="border p-2 font-bold">{m.type}</td>
                <td className="border p-2">
                  {m.month ? `${m.month}/` : ""}{m.year}
                </td>
                <td className="border p-2 text-right">
                  €{(m.totalAmount || m.annualVat || m.netVat || 0).toLocaleString()}
                </td>
                <td className="border p-2">
                  <span className={`px-2 py-1 rounded text-white text-xs ${
                    m.status === "Submitted" ? "bg-green-600" : 
                    m.status === "Generated" ? "bg-blue-600" : "bg-yellow-600"
                  }`}>
                    {m.status}
                  </span>
                </td>
                <td className="border p-2 text-xs">
                  <button className="text-blue-600 hover:underline mr-2">Descargar .TXT</button>
                  <button className="text-green-600 hover:underline">Enviar</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-6">
        <h3 className="font-bold mb-2">? Formato Oficial AEAT</h3>
        <p className="text-sm text-gray-700">
          Todos los modelos se generan en formato .txt oficial reconocido por AEAT.
          Descárgalos directamente desde aquí sin necesidad de conversión.
        </p>
      </div>

      <div className="flex gap-4">
        <button className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">
          Generar Modelo 347
        </button>
        <button className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          Generar Modelo 111
        </button>
        <button className="bg-purple-600 text-white px-4 py-2 rounded hover:bg-purple-700">
          Exportar Todos los Modelos
        </button>
      </div>
    </div>
  );
}
