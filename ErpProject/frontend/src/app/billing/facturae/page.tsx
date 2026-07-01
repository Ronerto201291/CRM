"use client";
import React, { useState } from "react";

export default function FacturaEPage() {
  const [documents] = useState([
    { id: 1, number: "FE-2025-001", status: "Signed", date: "2025-01-14", siiCompliant: true },
    { id: 2, number: "FE-2025-002", status: "Submitted", date: "2025-01-13", siiCompliant: true },
  ]);

  return (
    <div className="p-6">
      <h1 className="text-3xl font-bold mb-6">0.4 FacturaE / VERI*FACTU</h1>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <div className="border rounded-lg p-4 bg-blue-50">
          <p className="text-gray-600 text-sm">FacturaE 3.2.2</p>
          <p className="text-sm">Formato electrónico normalizado</p>
        </div>
        <div className="border rounded-lg p-4 bg-green-50">
          <p className="text-gray-600 text-sm">RD 1007/2023</p>
          <p className="text-sm">VERI*FACTU Compliance</p>
        </div>
        <div className="border rounded-lg p-4 bg-purple-50">
          <p className="text-gray-600 text-sm">Firma Digital</p>
          <p className="text-sm">XAdES-BES incluida</p>
        </div>
      </div>

      <div className="border rounded-lg p-4 mb-6">
        <h2 className="text-xl font-semibold mb-4">Documentos FacturaE Generados</h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-200">
              <th className="border p-2 text-left">Documento</th>
              <th className="border p-2 text-left">Fecha</th>
              <th className="border p-2">Estado</th>
              <th className="border p-2">VERI*FACTU</th>
              <th className="border p-2">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {documents.map((d) => (
              <tr key={d.id}>
                <td className="border p-2 font-mono">{d.number}</td>
                <td className="border p-2">{d.date}</td>
                <td className="border p-2">
                  <span className={`px-2 py-1 rounded text-white text-xs ${
                    d.status === "Submitted" ? "bg-green-600" : "bg-blue-600"
                  }`}>
                    {d.status}
                  </span>
                </td>
                <td className="border p-2">
                  {d.siiCompliant && <span className="bg-green-100 text-green-800 px-2 py-1 rounded text-xs">? Compliant</span>}
                </td>
                <td className="border p-2 text-xs space-x-1">
                  <button className="text-blue-600 hover:underline">Descargar XML</button>
                  <button className="text-green-600 hover:underline">Ver PDF</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div className="border rounded-lg p-4">
          <h3 className="font-bold mb-3">Representación Gráfica</h3>
          <ul className="text-sm space-y-2">
            <li>? PDF con QR de factura</li>
            <li>? HTML interactivo</li>
            <li>? Firma digital visible</li>
            <li>? Código de barras VERI*FACTU</li>
          </ul>
        </div>
        <div className="border rounded-lg p-4">
          <h3 className="font-bold mb-3">Seguridad & Cumplimiento</h3>
          <ul className="text-sm space-y-2">
            <li>? Firma XAdES-BES</li>
            <li>? Hash SHA256</li>
            <li>? Certificado FNMT</li>
            <li>? RD 1007/2023 compliant</li>
          </ul>
        </div>
      </div>

      <div className="mt-6 flex gap-4">
        <button className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">
          Generar FacturaE
        </button>
        <button className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          Firmar Digitalmente
        </button>
        <button className="bg-purple-600 text-white px-4 py-2 rounded hover:bg-purple-700">
          Enviar a VERI*FACTU
        </button>
      </div>
    </div>
  );
}
