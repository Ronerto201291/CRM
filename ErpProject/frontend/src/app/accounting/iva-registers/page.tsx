"use client";
import React, { useState } from "react";

export default function IvaManagementPage() {
  const [registers] = useState({
    purchaseTotal: 45000,
    salesTotal: 120000,
    purchaseRecords: 87,
    salesRecords: 125,
    intraEU: 15,
  });

  return (
    <div className="p-6">
      <h1 className="text-3xl font-bold mb-6">0.2 Libros IVA Exportables (RIVA + SII)</h1>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <div className="border rounded-lg p-4 bg-blue-50">
          <p className="text-gray-600 text-sm">Registro Compras</p>
          <p className="text-2xl font-bold">€{(registers.purchaseTotal / 1000).toFixed(0)}k</p>
          <p className="text-xs text-gray-500">{registers.purchaseRecords} registros</p>
        </div>
        <div className="border rounded-lg p-4 bg-green-50">
          <p className="text-gray-600 text-sm">Registro Ventas</p>
          <p className="text-2xl font-bold">€{(registers.salesTotal / 1000).toFixed(0)}k</p>
          <p className="text-xs text-gray-500">{registers.salesRecords} registros</p>
        </div>
        <div className="border rounded-lg p-4 bg-purple-50">
          <p className="text-gray-600 text-sm">Operaciones Intra-UE</p>
          <p className="text-2xl font-bold">{registers.intraEU}</p>
          <p className="text-xs text-gray-500">Triángulos incluidos</p>
        </div>
      </div>

      <div className="border rounded-lg p-4 mb-6">
        <h2 className="text-xl font-semibold mb-4">Exportar Libros Registro</h2>
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-bold">RIVA (Art. 63-66 LIVA)</h3>
              <p className="text-sm text-gray-600">Libro Registro IVA Aduanero - Formato oficial</p>
            </div>
            <button className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">
              Descargar .TXT
            </button>
          </div>
          <hr />
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-bold">SII (Sistema Inmediato de Información)</h3>
              <p className="text-sm text-gray-600">Envío directo a AEAT sin intermediarios</p>
            </div>
            <button className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
              Enviar a SII
            </button>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div className="border rounded-lg p-4">
          <h3 className="font-bold mb-3 text-lg">Libro Registro Compras</h3>
          <table className="w-full text-xs">
            <thead>
              <tr className="bg-gray-100">
                <th className="border p-1 text-left">Proveedor</th>
                <th className="border p-1 text-right">Base Imponible</th>
                <th className="border p-1 text-right">IVA</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td className="border p-1">Proveedor A</td>
                <td className="border p-1 text-right">€5.000</td>
                <td className="border p-1 text-right">€1.050</td>
              </tr>
              <tr>
                <td className="border p-1">Proveedor B (UE)</td>
                <td className="border p-1 text-right">€3.000</td>
                <td className="border p-1 text-right">€630</td>
              </tr>
              <tr className="bg-yellow-50 font-bold">
                <td className="border p-1">TOTAL</td>
                <td className="border p-1 text-right">€45.000</td>
                <td className="border p-1 text-right">€9.450</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div className="border rounded-lg p-4">
          <h3 className="font-bold mb-3 text-lg">Libro Registro Ventas</h3>
          <table className="w-full text-xs">
            <thead>
              <tr className="bg-gray-100">
                <th className="border p-1 text-left">Cliente</th>
                <th className="border p-1 text-right">Base Imponible</th>
                <th className="border p-1 text-right">IVA</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td className="border p-1">Cliente X</td>
                <td className="border p-1 text-right">€8.000</td>
                <td className="border p-1 text-right">€1.680</td>
              </tr>
              <tr>
                <td className="border p-1">Cliente Y (UE)</td>
                <td className="border p-1 text-right">€5.000</td>
                <td className="border p-1 text-right">€0 (ISP)</td>
              </tr>
              <tr className="bg-yellow-50 font-bold">
                <td className="border p-1">TOTAL</td>
                <td className="border p-1 text-right">€120.000</td>
                <td className="border p-1 text-right">€25.200</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div className="mt-6 bg-green-50 border border-green-200 rounded-lg p-4">
        <h3 className="font-bold mb-2">? Formatos Soportados</h3>
        <ul className="text-sm space-y-1">
          <li>? RIVA .txt oficial (Art. 63-66)</li>
          <li>? SII XML con certificado digital</li>
          <li>? Reverse Charge automático para ISP</li>
          <li>? Operaciones intra-UE identificadas</li>
        </ul>
      </div>
    </div>
  );
}
